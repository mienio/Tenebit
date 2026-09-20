using Microsoft.Extensions.Logging;
using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Audit;

namespace Tenebit.Application.Subscriptions;

/// <summary>
/// Periodically reconciles local billing state with Paddle's canonical subscription object. Webhooks
/// remain the fast path, while this closes gaps caused by delayed/lost delivery or operator changes.
/// Mismatched provider identifiers are quarantined instead of being applied to another tenant.
/// </summary>
public sealed class SubscriptionReconciliationService
{
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IActivityLogRepository _activity;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<SubscriptionReconciliationService> _logger;

    public SubscriptionReconciliationService(
        ISubscriptionRepository subscriptions,
        IPaymentGateway paymentGateway,
        IActivityLogRepository activity,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<SubscriptionReconciliationService> logger)
    {
        _subscriptions = subscriptions;
        _paymentGateway = paymentGateway;
        _activity = activity;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (!_paymentGateway.IsConfigured) return;

        var rows = await _subscriptions.ListWithPaddleSubscriptionAsync(cancellationToken);
        foreach (var subscription in rows)
        {
            var subscriptionId = subscription.PaddleSubscriptionId;
            if (string.IsNullOrWhiteSpace(subscriptionId)) continue;

            PaymentSubscriptionState? canonical;
            try
            {
                canonical = await _paymentGateway.GetSubscriptionAsync(subscriptionId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                SecurityTelemetry.ReconciliationFailure();
                _activity.Add(new ActivityLog(
                    subscription.OrganizationId,
                    "subscription.paddle_reconciliation_failed",
                    "subscription",
                    subscription.Id,
                    "paddle-reconciliation",
                    "canonical_fetch_failed",
                    _clock.UtcNow));
                continue;
            }

            var mismatch = canonical is null
                || !string.Equals(canonical.SubscriptionId, subscriptionId, StringComparison.Ordinal)
                || (!string.IsNullOrWhiteSpace(subscription.PaddleCustomerId)
                    && !string.Equals(canonical.CustomerId, subscription.PaddleCustomerId, StringComparison.Ordinal))
                || (canonical.OrganizationId.HasValue && canonical.OrganizationId.Value != subscription.OrganizationId);

            if (mismatch)
            {
                _activity.Add(new ActivityLog(
                    subscription.OrganizationId,
                    "subscription.paddle_reconciliation_mismatch",
                    "subscription",
                    subscription.Id,
                    "paddle-reconciliation",
                    "canonical_association_mismatch",
                    _clock.UtcNow));
                continue;
            }

            subscription.ReconcileFromPaddle(
                canonical!.PlanKey,
                canonical.BillingInterval,
                canonical.Status,
                canonical.CurrentPeriodStart,
                canonical.CurrentPeriodEnd,
                canonical.SubscriptionId,
                canonical.CustomerId);

            _activity.Add(new ActivityLog(
                subscription.OrganizationId,
                "subscription.paddle_reconciled",
                "subscription",
                subscription.Id,
                "paddle-reconciliation",
                $"{subscription.PlanKey}/{subscription.Status}",
                _clock.UtcNow));

            await RepairSubscriptionItemsAsync(subscription, cancellationToken);
        }

        await ReconcilePendingLinksAsync(cancellationToken);
        await AuditConfiguredPricesAsync(cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Heals a subscription that is carrying more than its one plan item. Plan changes already check this
    /// as they happen, but a subscription damaged earlier - by the switch path as it used to be, or by an
    /// edit in the Paddle dashboard - would otherwise sit there until it renewed at a multiple of its list
    /// price, which is what the 18-19.09.2026 report caught mid-flight (1 179 EUR queued for a 589,50 EUR
    /// plan). Never applies to a cancelled or unrecognized subscription: there is no canonical item to
    /// enforce, and rewriting one would be inventing entitlement rather than restoring it.
    /// </summary>
    private async Task RepairSubscriptionItemsAsync(Domain.Subscriptions.OrganizationSubscription subscription, CancellationToken cancellationToken)
    {
        if (!subscription.HasLivePaddleSubscription || subscription.PlanKey == Domain.Subscriptions.SubscriptionPlan.Free.Key) return;
        if (!_paymentGateway.IsPlanConfigured(subscription.PlanKey, subscription.BillingInterval)) return;

        try
        {
            var repair = await _paymentGateway.EnsureCanonicalItemsAsync(
                subscription.PaddleSubscriptionId!, subscription.PlanKey, subscription.BillingInterval, cancellationToken);
            if (repair.Repaired)
            {
                _activity.Add(new ActivityLog(
                    subscription.OrganizationId,
                    repair.Canonical ? "subscription.paddle_items_repaired" : "subscription.paddle_items_repair_failed",
                    "subscription",
                    subscription.Id,
                    "paddle-reconciliation",
                    repair.Detail,
                    _clock.UtcNow));
            }

            await AuditNextRenewalAsync(subscription, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            SecurityTelemetry.ReconciliationFailure();
            _activity.Add(new ActivityLog(
                subscription.OrganizationId,
                "subscription.paddle_reconciliation_failed",
                "subscription",
                subscription.Id,
                "paddle-reconciliation",
                "item_repair_failed",
                _clock.UtcNow));
        }
    }

    /// <summary>
    /// Reports a renewal that Paddle will bill above the plan's list price.
    ///
    /// Deferred proration is not a subscription item, so <see cref="IPaymentGateway.EnsureCanonicalItemsAsync"/>
    /// cannot rewrite it away, and Paddle has no API to drop a charge from a transaction it has not raised
    /// yet - the honest remedy in code is to make the overcharge visible before the invoice goes out, so it
    /// can be credited by hand. Nothing in the current switch path creates these (a change either bills
    /// immediately or not at all); what is still out there is residue from when a deferred downgrade was
    /// pushed to Paddle as full_next_billing_period, and it clears itself once billed.
    ///
    /// Logged rather than written to the activity feed on purpose: the condition persists until the renewal
    /// is billed, so an audit row per cycle would mean thousands of identical entries in one tenant's feed
    /// for a single fact that an operator has to act on once.
    /// </summary>
    private async Task AuditNextRenewalAsync(Domain.Subscriptions.OrganizationSubscription subscription, CancellationToken cancellationToken)
    {
        var audit = await _paymentGateway.GetRenewalAuditAsync(subscription.PaddleSubscriptionId!, cancellationToken);
        if (audit is null || audit.DeferredCharges.Count == 0) return;

        var extra = audit.DeferredCharges.Sum(line => line.Amount);
        var windows = string.Join(", ", audit.DeferredCharges.Select(line => $"{line.Amount:0.00} ({line.AccruedFrom:u} -> {line.AccruedTo:u})"));

        _logger.LogError(
            "Paddle subscription {SubscriptionId} (org {OrganizationId}) renews on {RenewsAt} at {Total} {Currency}, which includes {Extra} {Currency} of deferred proration not part of the {PlanKey} list price: {Windows}. Paddle cannot drop these from a future transaction - credit the customer by hand if the renewal has not been billed yet.",
            subscription.PaddleSubscriptionId, subscription.OrganizationId, audit.BillingPeriodStart, audit.Total,
            audit.Currency, extra, audit.Currency, subscription.PlanKey, windows);
    }
    /// <summary>How long before its effective date a scheduled downgrade is pushed to Paddle. Paddle raises
    /// the renewal invoice at the period end, and the swap has to be in place by then or the org pays for
    /// another full period of the plan it asked to leave - so this errs on the early side. Must stay
    /// comfortably larger than the job's own interval.</summary>
    public static readonly TimeSpan PlanChangeLead = TimeSpan.FromMinutes(20);

    /// <summary>
    /// Pushes downgrades whose effective date has arrived onto Paddle - the second half of the deferred
    /// switch that <see cref="SubscriptionService.ChangePlanAsync"/> only records locally (Paddle cannot
    /// schedule a plan change itself; see OrganizationSubscription.PendingPlanKey).
    ///
    /// Runs on a much tighter cadence than <see cref="RunAsync"/> because it is time-critical, and is safe
    /// to re-run: a change that already landed clears its own pending state and a failed one is simply
    /// retried on the next pass.
    /// </summary>
    public async Task ApplyDuePlanChangesAsync(CancellationToken cancellationToken)
    {
        if (!_paymentGateway.IsConfigured) return;

        var now = _clock.UtcNow;
        var due = await _subscriptions.ListDuePlanChangesAsync(now + PlanChangeLead, cancellationToken);

        foreach (var subscription in due)
        {
            if (!subscription.IsPlanChangeDue(now, PlanChangeLead)) continue;

            var planKey = subscription.PendingPlanKey!;
            var interval = subscription.PendingBillingInterval ?? subscription.BillingInterval;
            var subscriptionId = subscription.PaddleSubscriptionId;

            // Nothing left to switch: the subscription is gone, or an upgrade already moved the org onto
            // the plan that was scheduled.
            if (string.IsNullOrWhiteSpace(subscriptionId) || !subscription.HasLivePaddleSubscription)
            {
                subscription.ClearPendingPlanChange();
                LogPlanChange(subscription, "subscription.plan_change_abandoned", $"{planKey}/{interval}: no live Paddle subscription");
                continue;
            }

            if (subscription.PlanKey == planKey && subscription.BillingInterval == interval)
            {
                subscription.ClearPendingPlanChange();
                LogPlanChange(subscription, "subscription.plan_change_noop", $"already on {planKey}/{interval}");
                continue;
            }

            // The target was configured when the owner scheduled it; if its Paddle Price has since been
            // removed from configuration there is nothing to switch to, and retrying every five minutes
            // forever would only hammer Paddle. Held (not dropped) so it resumes once the price is back -
            // meanwhile the org simply stays on the plan it is paying for.
            if (!_paymentGateway.IsPlanConfigured(planKey, interval))
            {
                // Operator-level misconfiguration, and this pass repeats every few minutes - it belongs in
                // the logs, not as a new audit row per attempt in somebody's activity feed.
                _logger.LogError(
                    "Scheduled switch to {PlanKey}/{Interval} for subscription {SubscriptionId} cannot be applied: no Paddle price is configured for it.",
                    planKey, interval, subscriptionId);
                continue;
            }

            try
            {
                // A switch that keeps the billing cycle needs no money to move: the item swap alone makes
                // the next renewal bill the new plan, at the anchor the customer already has. A switch that
                // changes the cycle has to start a new period, so it is prorated and charged now - by
                // construction that happens at (or just before) the old period's end, where the credit for
                // the remainder is ~zero. The same immediate mode also repairs the case where this job was
                // late and Paddle already renewed on the old plan: that renewal is then credited back.
                var renewedAlready = subscription.CurrentPeriodEnd > subscription.PendingPlanEffectiveAt!.Value;
                var billing = interval != subscription.BillingInterval || renewedAlready
                    ? PlanChangeBilling.ProrateImmediately
                    : PlanChangeBilling.SwitchWithoutCharge;

                var result = await _paymentGateway.ChangeSubscriptionPlanAsync(
                    subscriptionId,
                    planKey,
                    interval,
                    billing,
                    $"tenebit-planchange-due-{subscriptionId}-{planKey}-{interval}-{subscription.PendingPlanEffectiveAt:yyyyMMddHHmm}",
                    cancellationToken);

                var canonical = result.Subscription;
                if (!string.Equals(canonical.SubscriptionId, subscriptionId, StringComparison.Ordinal)
                    || (canonical.OrganizationId.HasValue && canonical.OrganizationId.Value != subscription.OrganizationId))
                {
                    LogPlanChange(subscription, "subscription.paddle_reconciliation_mismatch", "canonical_association_mismatch");
                    continue;
                }

                subscription.ApplyPendingPlanChange();
                subscription.ReconcileFromPaddle(
                    canonical.PlanKey, canonical.BillingInterval, canonical.Status,
                    canonical.CurrentPeriodStart, canonical.CurrentPeriodEnd, canonical.SubscriptionId, canonical.CustomerId);

                LogPlanChange(subscription, "subscription.plan_change_applied", $"{planKey}/{interval} ({billing})");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                // Left pending on purpose: the org keeps the plan it paid for and the next pass retries.
                SecurityTelemetry.ReconciliationFailure();
                LogPlanChange(subscription, "subscription.plan_change_failed", $"{planKey}/{interval}");
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private void LogPlanChange(Domain.Subscriptions.OrganizationSubscription subscription, string action, string detail) =>
        _activity.Add(new ActivityLog(
            subscription.OrganizationId, action, "subscription", subscription.Id, "paddle-reconciliation", detail, _clock.UtcNow));

    /// <summary>Catches a configured Paddle Price drifting away from the price the pricing page advertises -
    /// a dashboard typo charges every customer on that plan the wrong amount indefinitely and shows up
    /// nowhere else (błąd 4: Growth Annual billed 252,65 EUR against the advertised 289,50 EUR).</summary>
    private async Task AuditConfiguredPricesAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<PlanPriceMismatch> mismatches;
        try
        {
            mismatches = await _paymentGateway.ListPlanPriceMismatchesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            SecurityTelemetry.ReconciliationFailure();
            return;
        }

        // Operator-facing, not tenant-facing: this is a misconfiguration of the product's own price list,
        // so it belongs in the logs rather than in some organization's activity feed.
        foreach (var mismatch in mismatches)
        {
            SecurityTelemetry.ReconciliationFailure();
            _logger.LogError(
                "Paddle price {PriceId} for {PlanKey}/{Interval} does not match the plan catalogue ({Reason}): expected {Expected} {Currency}, Paddle has {Actual}. Customers are being billed the Paddle amount.",
                mismatch.PriceId, mismatch.PlanKey, mismatch.Interval, mismatch.Reason, mismatch.Expected, mismatch.Currency, mismatch.Actual);
        }
    }

    /// <summary>Discovers subscriptions Paddle knows about for organizations that started billing (have a
    /// customer) but never got their PaddleSubscriptionId linked - the gap a lost or rejected
    /// created-subscription webhook leaves behind. A customer with no Paddle subscription at all (never
    /// checked out, or cancelled without ever completing one) is the ordinary case, not a failure.</summary>
    private async Task ReconcilePendingLinksAsync(CancellationToken cancellationToken)
    {
        var pending = await _subscriptions.ListPendingPaddleLinkAsync(cancellationToken);
        foreach (var subscription in pending)
        {
            PaymentSubscriptionState? canonical;
            try
            {
                canonical = await _paymentGateway.FindSubscriptionByCustomerAsync(subscription.PaddleCustomerId!, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                SecurityTelemetry.ReconciliationFailure();
                _activity.Add(new ActivityLog(
                    subscription.OrganizationId,
                    "subscription.paddle_reconciliation_failed",
                    "subscription",
                    subscription.Id,
                    "paddle-reconciliation",
                    "customer_lookup_failed",
                    _clock.UtcNow));
                continue;
            }

            if (canonical is null) continue;

            var mismatch = !string.Equals(canonical.CustomerId, subscription.PaddleCustomerId, StringComparison.Ordinal)
                || (canonical.OrganizationId.HasValue && canonical.OrganizationId.Value != subscription.OrganizationId);
            if (mismatch)
            {
                _activity.Add(new ActivityLog(
                    subscription.OrganizationId,
                    "subscription.paddle_reconciliation_mismatch",
                    "subscription",
                    subscription.Id,
                    "paddle-reconciliation",
                    "canonical_association_mismatch",
                    _clock.UtcNow));
                continue;
            }

            subscription.ReconcileFromPaddle(
                canonical.PlanKey, canonical.BillingInterval, canonical.Status, canonical.CurrentPeriodStart, canonical.CurrentPeriodEnd,
                canonical.SubscriptionId, canonical.CustomerId);

            _activity.Add(new ActivityLog(
                subscription.OrganizationId,
                "subscription.paddle_reconciled",
                "subscription",
                subscription.Id,
                "paddle-reconciliation",
                $"{subscription.PlanKey}/{subscription.Status}",
                _clock.UtcNow));
        }
    }
}
