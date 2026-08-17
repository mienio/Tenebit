using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Common;
using Tenebit.Domain.Subscriptions;

namespace Tenebit.Application.Subscriptions;

public sealed class SubscriptionService
{
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IProcessedStripeEventRepository _processedEvents;
    private readonly IAssetRepository _assets;
    private readonly IActivityLogRepository _activity;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentGateway _paymentGateway;

    public SubscriptionService(
        ISubscriptionRepository subscriptions,
        IProcessedStripeEventRepository processedEvents,
        IAssetRepository assets,
        IActivityLogRepository activity,
        ICurrentUser currentUser,
        IClock clock,
        IUnitOfWork unitOfWork,
        IPaymentGateway paymentGateway)
    {
        _subscriptions = subscriptions;
        _processedEvents = processedEvents;
        _assets = assets;
        _activity = activity;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _paymentGateway = paymentGateway;
    }

    public async Task<Result<SubscriptionResponse>> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var subscription = await _subscriptions.GetByOrganizationAsync(_currentUser.OrganizationId, cancellationToken);

        if (subscription is null)
        {
            // Create default Free subscription
            subscription = new OrganizationSubscription(_currentUser.OrganizationId, SubscriptionPlan.Free.Key);
            _subscriptions.Add(subscription);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var assetCount = await _assets.CountAsync(_currentUser.OrganizationId, cancellationToken);
        var plan = SubscriptionPlan.FromKey(subscription.PlanKey) ?? SubscriptionPlan.Free;

        return Result<SubscriptionResponse>.Success(new SubscriptionResponse(
            subscription.Id,
            subscription.PlanKey,
            plan.Name,
            plan.AssetLimit,
            plan.MonthlyPrice,
            plan.Currency,
            assetCount,
            subscription.Status.ToString(),
            subscription.CurrentPeriodEnd
        ));
    }

    /// <summary>
    /// Direct, no-billing plan switch. Only the Free plan can be reached this way — moving to a paid
    /// plan requires real payment via <see cref="CreateCheckoutSessionAsync"/>. Downgrading away from an
    /// active Stripe subscription must go through the Stripe billing portal so cancellation actually
    /// stops the charges instead of just editing our own record.
    /// </summary>
    public async Task<Result<SubscriptionResponse>> UpgradeAsync(string planKey, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner);
        if (access.IsFailure) return Result<SubscriptionResponse>.Failure(access.Error!);

        var newPlan = SubscriptionPlan.FromKey(planKey);
        if (newPlan is null)
        {
            return Result<SubscriptionResponse>.Failure(Error.Validation($"Unknown plan: {planKey}"));
        }

        if (newPlan.Key != SubscriptionPlan.Free.Key)
        {
            return Result<SubscriptionResponse>.Failure(Error.Validation("Aby przejść na plan Pro, użyj płatności Stripe (checkout)."));
        }

        try
        {
            var subscription = await _subscriptions.GetByOrganizationAsync(_currentUser.OrganizationId, cancellationToken);

            if (subscription is null)
            {
                subscription = new OrganizationSubscription(_currentUser.OrganizationId, planKey);
                _subscriptions.Add(subscription);
            }
            else
            {
                if (subscription.HasActiveStripeSubscription)
                {
                    return Result<SubscriptionResponse>.Failure(Error.Validation("Ta organizacja ma aktywną płatną subskrypcję. Zarządzaj nią (w tym anulowaniem) w portalu rozliczeń Stripe."));
                }

                subscription.Upgrade(planKey);
            }

            _activity.Add(new ActivityLog(
                _currentUser.OrganizationId,
                "subscription.upgraded",
                "subscription",
                subscription.Id,
                _currentUser.Subject,
                $"Upgraded to {newPlan.Name}",
                _clock.UtcNow));

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var assetCount = await _assets.CountAsync(_currentUser.OrganizationId, cancellationToken);

            return Result<SubscriptionResponse>.Success(new SubscriptionResponse(
                subscription.Id,
                subscription.PlanKey,
                newPlan.Name,
                newPlan.AssetLimit,
                newPlan.MonthlyPrice,
                newPlan.Currency,
                assetCount,
                subscription.Status.ToString(),
                subscription.CurrentPeriodEnd
            ));
        }
        catch (DomainException ex)
        {
            return Result<SubscriptionResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    /// <summary>Starts a real Stripe Checkout flow for the Pro plan and returns the hosted checkout URL to redirect to.</summary>
    public async Task<Result<string>> CreateCheckoutSessionAsync(string successUrl, string cancelUrl, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner);
        if (access.IsFailure) return Result<string>.Failure(access.Error!);

        if (!_paymentGateway.IsConfigured)
        {
            return Result<string>.Failure(Error.Validation("Płatności Stripe nie są jeszcze skonfigurowane."));
        }

        var organizationId = _currentUser.OrganizationId;
        var subscription = await _subscriptions.GetByOrganizationAsync(organizationId, cancellationToken);
        if (subscription is null)
        {
            subscription = new OrganizationSubscription(organizationId, SubscriptionPlan.Free.Key);
            _subscriptions.Add(subscription);
        }

        if (subscription.HasActiveStripeSubscription)
        {
            return Result<string>.Failure(Error.Validation("Organizacja ma już aktywną płatną subskrypcję."));
        }

        if (string.IsNullOrWhiteSpace(subscription.StripeCustomerId))
        {
            var customerId = await _paymentGateway.CreateCustomerAsync(_currentUser.Email, organizationId, cancellationToken);
            subscription.AttachStripeCustomer(customerId);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var checkoutUrl = await _paymentGateway.CreateCheckoutSessionAsync(subscription.StripeCustomerId!, organizationId, successUrl, cancelUrl, cancellationToken);
        return Result<string>.Success(checkoutUrl);
    }

    /// <summary>Opens the Stripe Billing Portal so the owner can manage payment method, invoices, or cancel.</summary>
    public async Task<Result<string>> CreateBillingPortalSessionAsync(string returnUrl, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner);
        if (access.IsFailure) return Result<string>.Failure(access.Error!);

        if (!_paymentGateway.IsConfigured)
        {
            return Result<string>.Failure(Error.Validation("Płatności Stripe nie są jeszcze skonfigurowane."));
        }

        var subscription = await _subscriptions.GetByOrganizationAsync(_currentUser.OrganizationId, cancellationToken);
        if (string.IsNullOrWhiteSpace(subscription?.StripeCustomerId))
        {
            return Result<string>.Failure(Error.Validation("Organizacja nie ma jeszcze konta rozliczeniowego Stripe."));
        }

        var url = await _paymentGateway.CreateBillingPortalSessionAsync(subscription.StripeCustomerId, returnUrl, cancellationToken);
        return Result<string>.Success(url);
    }

    /// <summary>Handles Stripe's customer.subscription.created/updated/deleted webhooks and syncs our own record.</summary>
    public async Task<Result> HandleWebhookAsync(string payload, string signatureHeader, CancellationToken cancellationToken)
    {
        PaymentWebhookEvent? webhookEvent;
        try
        {
            webhookEvent = _paymentGateway.ParseWebhookEvent(payload, signatureHeader);
        }
        catch (Exception)
        {
            return Result.Failure(Error.Validation("Nieprawidłowy podpis webhooka Stripe."));
        }

        if (webhookEvent is null) return Result.Success();

        // Stripe retries webhook delivery on timeout/5xx — replaying the same EventId must be a no-op
        // instead of reapplying (and re-logging) the same state change twice (audyt P0.6).
        if (await _processedEvents.ExistsAsync(webhookEvent.EventId, cancellationToken))
        {
            return Result.Success();
        }

        _processedEvents.Add(new ProcessedStripeEvent(webhookEvent.EventId, _clock.UtcNow));

        var subscription = webhookEvent.OrganizationId.HasValue
            ? await _subscriptions.GetByOrganizationAsync(webhookEvent.OrganizationId.Value, cancellationToken)
            : await _subscriptions.GetByStripeCustomerAsync(webhookEvent.CustomerId, cancellationToken);

        if (subscription is null)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        // Stripe does not guarantee delivery order — an older event arriving after a newer one (e.g. a
        // delayed retry) must never overwrite state the newer event already applied (audyt P0.6).
        if (subscription.LastWebhookEventAt is { } lastEventAt && webhookEvent.EventCreatedAt <= lastEventAt)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        subscription.SyncFromStripe(webhookEvent.PlanKey, webhookEvent.Status, webhookEvent.CurrentPeriodStart, webhookEvent.CurrentPeriodEnd, webhookEvent.SubscriptionId, webhookEvent.CustomerId, webhookEvent.EventCreatedAt);

        _activity.Add(new ActivityLog(
            subscription.OrganizationId,
            "subscription.stripe_synced",
            "subscription",
            subscription.Id,
            "stripe-webhook",
            $"{webhookEvent.EventType}: {subscription.PlanKey}/{subscription.Status}",
            _clock.UtcNow));

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<bool>> CanAddAssetAsync(CancellationToken cancellationToken)
    {
        var subscription = await _subscriptions.GetByOrganizationAsync(_currentUser.OrganizationId, cancellationToken);

        if (subscription is null)
        {
            // No subscription = Free plan
            subscription = new OrganizationSubscription(_currentUser.OrganizationId, SubscriptionPlan.Free.Key);
            _subscriptions.Add(subscription);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var assetCount = await _assets.CountAsync(_currentUser.OrganizationId, cancellationToken);
        var limit = subscription.GetAssetLimit();

        return Result<bool>.Success(assetCount < limit);
    }
}

public sealed record SubscriptionResponse(
    Guid Id,
    string PlanKey,
    string PlanName,
    int AssetLimit,
    decimal MonthlyPrice,
    string Currency,
    int CurrentAssetCount,
    string Status,
    DateTimeOffset CurrentPeriodEnd
);
