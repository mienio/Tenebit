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
        }

        await ReconcilePendingLinksAsync(cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
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
                canonical.PlanKey, canonical.Status, canonical.CurrentPeriodStart, canonical.CurrentPeriodEnd,
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
