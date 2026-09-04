using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Audit;

namespace Tenebit.Application.Subscriptions;

/// <summary>
/// Periodically reconciles local billing state with Stripe's canonical subscription object. Webhooks
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

    public SubscriptionReconciliationService(
        ISubscriptionRepository subscriptions,
        IPaymentGateway paymentGateway,
        IActivityLogRepository activity,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _subscriptions = subscriptions;
        _paymentGateway = paymentGateway;
        _activity = activity;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (!_paymentGateway.IsConfigured) return;

        var rows = await _subscriptions.ListWithStripeSubscriptionAsync(cancellationToken);
        foreach (var subscription in rows)
        {
            var subscriptionId = subscription.StripeSubscriptionId;
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
                    "subscription.stripe_reconciliation_failed",
                    "subscription",
                    subscription.Id,
                    "stripe-reconciliation",
                    "canonical_fetch_failed",
                    _clock.UtcNow));
                continue;
            }

            var mismatch = canonical is null
                || !string.Equals(canonical.SubscriptionId, subscriptionId, StringComparison.Ordinal)
                || (!string.IsNullOrWhiteSpace(subscription.StripeCustomerId)
                    && !string.Equals(canonical.CustomerId, subscription.StripeCustomerId, StringComparison.Ordinal))
                || (canonical.OrganizationId.HasValue && canonical.OrganizationId.Value != subscription.OrganizationId);

            if (mismatch)
            {
                _activity.Add(new ActivityLog(
                    subscription.OrganizationId,
                    "subscription.stripe_reconciliation_mismatch",
                    "subscription",
                    subscription.Id,
                    "stripe-reconciliation",
                    "canonical_association_mismatch",
                    _clock.UtcNow));
                continue;
            }

            subscription.ReconcileFromStripe(
                canonical!.PlanKey,
                canonical.Status,
                canonical.CurrentPeriodStart,
                canonical.CurrentPeriodEnd,
                canonical.SubscriptionId,
                canonical.CustomerId);

            _activity.Add(new ActivityLog(
                subscription.OrganizationId,
                "subscription.stripe_reconciled",
                "subscription",
                subscription.Id,
                "stripe-reconciliation",
                $"{subscription.PlanKey}/{subscription.Status}",
                _clock.UtcNow));
        }

        await ReconcilePendingLinksAsync(cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Discovers subscriptions Stripe knows about for organizations that started billing (have a
    /// customer) but never got their StripeSubscriptionId linked - the gap a lost or rejected
    /// created-subscription webhook leaves behind. A customer with no Stripe subscription at all (never
    /// checked out, or cancelled without ever completing one) is the ordinary case, not a failure.</summary>
    private async Task ReconcilePendingLinksAsync(CancellationToken cancellationToken)
    {
        var pending = await _subscriptions.ListPendingStripeLinkAsync(cancellationToken);
        foreach (var subscription in pending)
        {
            PaymentSubscriptionState? canonical;
            try
            {
                canonical = await _paymentGateway.FindSubscriptionByCustomerAsync(subscription.StripeCustomerId!, cancellationToken);
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
                    "subscription.stripe_reconciliation_failed",
                    "subscription",
                    subscription.Id,
                    "stripe-reconciliation",
                    "customer_lookup_failed",
                    _clock.UtcNow));
                continue;
            }

            if (canonical is null) continue;

            var mismatch = !string.Equals(canonical.CustomerId, subscription.StripeCustomerId, StringComparison.Ordinal)
                || (canonical.OrganizationId.HasValue && canonical.OrganizationId.Value != subscription.OrganizationId);
            if (mismatch)
            {
                _activity.Add(new ActivityLog(
                    subscription.OrganizationId,
                    "subscription.stripe_reconciliation_mismatch",
                    "subscription",
                    subscription.Id,
                    "stripe-reconciliation",
                    "canonical_association_mismatch",
                    _clock.UtcNow));
                continue;
            }

            subscription.ReconcileFromStripe(
                canonical.PlanKey, canonical.Status, canonical.CurrentPeriodStart, canonical.CurrentPeriodEnd,
                canonical.SubscriptionId, canonical.CustomerId);

            _activity.Add(new ActivityLog(
                subscription.OrganizationId,
                "subscription.stripe_reconciled",
                "subscription",
                subscription.Id,
                "stripe-reconciliation",
                $"{subscription.PlanKey}/{subscription.Status}",
                _clock.UtcNow));
        }
    }
}
