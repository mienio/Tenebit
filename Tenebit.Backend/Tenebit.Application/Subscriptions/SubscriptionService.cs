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
    private readonly IAppLinkBuilder _appLinkBuilder;
    private readonly IPromoCodeRepository _promoCodes;

    public SubscriptionService(
        ISubscriptionRepository subscriptions,
        IProcessedStripeEventRepository processedEvents,
        IAssetRepository assets,
        IActivityLogRepository activity,
        ICurrentUser currentUser,
        IClock clock,
        IUnitOfWork unitOfWork,
        IPaymentGateway paymentGateway,
        IAppLinkBuilder appLinkBuilder,
        IPromoCodeRepository promoCodes)
    {
        _subscriptions = subscriptions;
        _processedEvents = processedEvents;
        _assets = assets;
        _activity = activity;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _paymentGateway = paymentGateway;
        _appLinkBuilder = appLinkBuilder;
        _promoCodes = promoCodes;
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

        var plan = SubscriptionPlan.FromKey(subscription.PlanKey) ?? SubscriptionPlan.Free;
        var usage = await BuildUsageAsync(subscription, cancellationToken);
        var assetCount = usage.First(x => x.Resource == "assets").Current;

        return Result<SubscriptionResponse>.Success(new SubscriptionResponse(
            subscription.Id,
            subscription.PlanKey,
            plan.Name,
            plan.AssetLimit,
            plan.MonthlyPrice,
            plan.Currency,
            assetCount,
            subscription.Status.ToString(),
            subscription.CurrentPeriodEnd,
            usage
        ));
    }

    /// <summary>Na zewnątrz raportujemy wyłącznie licznik aktywów. Limity osób, procedur, licencji,
    /// lokalizacji, zespołów, profili i kategorii nadal obowiązują i są egzekwowane przy tworzeniu
    /// każdego z tych rekordów (<see cref="OrganizationSubscription.GetResourceLimit"/>) - po prostu
    /// nie wystawiamy ich liczników; opisuje je regulamin.</summary>
    private async Task<IReadOnlyList<ResourceUsage>> BuildUsageAsync(OrganizationSubscription subscription, CancellationToken cancellationToken)
    {
        var assetCount = await _assets.CountAsync(_currentUser.OrganizationId, cancellationToken);

        return [new ResourceUsage("assets", assetCount, subscription.GetResourceLimit())];
    }

    /// <summary>
    /// Direct, no-billing plan switch. Only the Free plan can be reached this way - moving to a paid
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
            return Result<SubscriptionResponse>.Failure(Error.Validation($"Aby przejść na plan {newPlan.Name}, użyj płatności Stripe (checkout)."));
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
                if (subscription.HasLiveStripeSubscription)
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

            var usage = await BuildUsageAsync(subscription, cancellationToken);

            return Result<SubscriptionResponse>.Success(new SubscriptionResponse(
                subscription.Id,
                subscription.PlanKey,
                newPlan.Name,
                newPlan.AssetLimit,
                newPlan.MonthlyPrice,
                newPlan.Currency,
                usage.First(x => x.Resource == "assets").Current,
                subscription.Status.ToString(),
                subscription.CurrentPeriodEnd,
                usage
            ));
        }
        catch (DomainException ex)
        {
            return Result<SubscriptionResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    /// <summary>Looks up a promo code for the given plan without redeeming it - used to show the discounted
    /// price in the checkout dialog before the customer commits to paying.</summary>
    public async Task<Result<PromoCodeValidationResponse>> ValidatePromoCodeAsync(string planKey, string code, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner);
        if (access.IsFailure) return Result<PromoCodeValidationResponse>.Failure(access.Error!);

        var plan = SubscriptionPlan.FromKey(planKey);
        if (plan is null || plan.Key == SubscriptionPlan.Free.Key)
            return Result<PromoCodeValidationResponse>.Failure(Error.Validation($"Unknown plan: {planKey}"));

        var promo = string.IsNullOrWhiteSpace(code) ? null : await _promoCodes.GetByCodeAsync(code, cancellationToken);
        if (promo is null || promo.PlanKey != plan.Key || !promo.IsUsable(_clock.UtcNow))
            return Result<PromoCodeValidationResponse>.Failure(Error.Validation("Kod promocyjny jest nieprawidłowy lub wygasł."));

        return Result<PromoCodeValidationResponse>.Success(new PromoCodeValidationResponse(
            promo.Code, promo.DiscountType.ToString(), promo.DiscountValue, plan.MonthlyPrice, promo.ApplyTo(plan.MonthlyPrice), plan.Currency));
    }

    /// <summary>Starts a real Stripe Checkout flow for the given paid plan and returns the hosted checkout URL to redirect to.</summary>
    public async Task<Result<string>> CreateCheckoutSessionAsync(string planKey, string successPath, string cancelPath, CancellationToken cancellationToken, string? promoCode = null)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner);
        if (access.IsFailure) return Result<string>.Failure(access.Error!);

        var targetPlan = SubscriptionPlan.FromKey(planKey);
        if (targetPlan is null || targetPlan.Key == SubscriptionPlan.Free.Key)
            return Result<string>.Failure(Error.Validation($"Unknown plan: {planKey}"));

        if (!_paymentGateway.IsConfigured || !_paymentGateway.IsPlanConfigured(targetPlan.Key))
            return Result<string>.Failure(Error.Validation("Płatności Stripe nie są jeszcze skonfigurowane dla tego planu."));

        PromoCodeDiscount? discount = null;
        if (!string.IsNullOrWhiteSpace(promoCode))
        {
            var promo = await _promoCodes.GetByCodeAsync(promoCode, cancellationToken);
            if (promo is null || promo.PlanKey != targetPlan.Key || !promo.IsUsable(_clock.UtcNow))
                return Result<string>.Failure(Error.Validation("Kod promocyjny jest nieprawidłowy lub wygasł."));

            promo.Redeem();
            discount = new PromoCodeDiscount(promo.DiscountType, promo.DiscountValue);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var organizationId = _currentUser.OrganizationId;
        var subscription = await _subscriptions.GetByOrganizationAsync(organizationId, cancellationToken);
        if (subscription is null)
        {
            subscription = new OrganizationSubscription(organizationId, SubscriptionPlan.Free.Key);
            _subscriptions.Add(subscription);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(subscription.StripeSubscriptionId))
        {
            var canonical = await _paymentGateway.GetSubscriptionAsync(subscription.StripeSubscriptionId, cancellationToken)
                ?? throw new PaymentGatewayException("Stripe subscription state could not be verified.");
            if (!string.Equals(canonical.CustomerId, subscription.StripeCustomerId, StringComparison.Ordinal)
                || (canonical.OrganizationId.HasValue && canonical.OrganizationId.Value != organizationId))
                throw new PaymentGatewayException("Stripe subscription association mismatch.");

            subscription.ReconcileFromStripe(canonical.PlanKey, canonical.Status, canonical.CurrentPeriodStart, canonical.CurrentPeriodEnd, canonical.SubscriptionId, canonical.CustomerId);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (canonical.Status != SubscriptionStatus.Cancelled)
                return Result<string>.Failure(Error.Validation("Istnieje subskrypcja Stripe wymagająca naprawy lub zarządzania. Użyj portalu rozliczeniowego zamiast tworzyć drugą subskrypcję."));
        }

        if (string.IsNullOrWhiteSpace(subscription.StripeCustomerId))
        {
            var customerId = await _paymentGateway.CreateCustomerAsync(
                _currentUser.Email, organizationId, $"tenebit-customer-{organizationId:N}", cancellationToken);
            subscription.AttachStripeCustomer(customerId);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var attemptId = await _unitOfWork.ExecuteWithResourceLocksAsync(
            organizationId,
            "subscription-checkout",
            [organizationId],
            async ct =>
        {
            var locked = await _subscriptions.GetByOrganizationAsync(organizationId, ct)
                ?? throw new InvalidOperationException("Subscription disappeared during checkout claim.");
            var id = locked.GetOrCreateCheckoutAttempt(_clock.UtcNow, TimeSpan.FromMinutes(30));
            await _unitOfWork.SaveChangesAsync(ct);
            return id;
        }, cancellationToken);

        var checkoutUrl = await _paymentGateway.CreateCheckoutSessionAsync(
            subscription.StripeCustomerId!,
            organizationId,
            targetPlan.Key,
            _appLinkBuilder.BuildAppUrl(successPath),
            _appLinkBuilder.BuildAppUrl(cancelPath),
            $"tenebit-checkout-{attemptId:N}",
            cancellationToken,
            discount);
        return Result<string>.Success(checkoutUrl);
    }

    /// <summary>Opens the Stripe Billing Portal so the owner can manage payment method, invoices, or cancel.</summary>
    public async Task<Result<string>> CreateBillingPortalSessionAsync(string returnPath, CancellationToken cancellationToken)
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

        var returnUrl = _appLinkBuilder.BuildAppUrl(returnPath);
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
        catch (PaymentWebhookValidationException)
        {
            SecurityTelemetry.WebhookRejected();
            return Result.Failure(Error.Validation("Nieprawidłowy webhook Stripe."));
        }

        if (webhookEvent is null) return Result.Success();

        // Stripe retries webhook delivery on timeout/5xx - replaying the same EventId must be a no-op
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

        // Metadata routing is a convenience, not proof of ownership - once a subscription already has a
        // Stripe customer attached (from our own CreateCheckoutSessionAsync flow), an event whose
        // customer/subscription IDs don't match that record must not be applied to it. Without this, a
        // crafted or misrouted metadata.organizationId could point one organization's webhook event at
        // another organization's subscription record (audyt AUD3-010).
        var customerMismatch = !string.IsNullOrWhiteSpace(subscription.StripeCustomerId)
            && !string.Equals(subscription.StripeCustomerId, webhookEvent.CustomerId, StringComparison.Ordinal);
        var subscriptionMismatch = !string.IsNullOrWhiteSpace(subscription.StripeSubscriptionId)
            && !string.IsNullOrWhiteSpace(webhookEvent.SubscriptionId)
            && !string.Equals(subscription.StripeSubscriptionId, webhookEvent.SubscriptionId, StringComparison.Ordinal);
        if (customerMismatch || subscriptionMismatch)
        {
            _activity.Add(new ActivityLog(
                subscription.OrganizationId,
                "subscription.stripe_association_mismatch",
                "subscription",
                subscription.Id,
                "stripe-webhook",
                $"event={webhookEvent.EventId} customer={webhookEvent.CustomerId} subscription={webhookEvent.SubscriptionId}",
                _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        var appliedPlan = webhookEvent.PlanKey;
        var appliedStatus = webhookEvent.Status;
        var appliedStart = webhookEvent.CurrentPeriodStart;
        var appliedEnd = webhookEvent.CurrentPeriodEnd;
        var appliedSubscriptionId = webhookEvent.SubscriptionId;
        var appliedCustomerId = webhookEvent.CustomerId;

        if (webhookEvent.EventType != "customer.subscription.deleted" && !string.IsNullOrWhiteSpace(webhookEvent.SubscriptionId))
        {
            var canonical = await _paymentGateway.GetSubscriptionAsync(webhookEvent.SubscriptionId, cancellationToken)
                ?? throw new PaymentGatewayException("Stripe canonical subscription was not found.");
            if (!string.Equals(canonical.CustomerId, webhookEvent.CustomerId, StringComparison.Ordinal)
                || !string.Equals(canonical.SubscriptionId, webhookEvent.SubscriptionId, StringComparison.Ordinal)
                || (canonical.OrganizationId.HasValue && canonical.OrganizationId != subscription.OrganizationId))
            {
                throw new PaymentGatewayException("Stripe canonical association mismatch.");
            }
            appliedPlan = canonical.PlanKey; appliedStatus = canonical.Status; appliedStart = canonical.CurrentPeriodStart;
            appliedEnd = canonical.CurrentPeriodEnd; appliedSubscriptionId = canonical.SubscriptionId; appliedCustomerId = canonical.CustomerId;
        }
        else if (subscription.LastWebhookEventAt is { } lastEventAt && webhookEvent.EventCreatedAt < lastEventAt)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        subscription.SyncFromStripe(appliedPlan, appliedStatus, appliedStart, appliedEnd, appliedSubscriptionId, appliedCustomerId, webhookEvent.EventCreatedAt);

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
    DateTimeOffset CurrentPeriodEnd,
    IReadOnlyList<ResourceUsage> Usage
);

public sealed record ResourceUsage(string Resource, int Current, int Limit);

public sealed record PromoCodeValidationResponse(
    string Code, string DiscountType, decimal DiscountValue, decimal OriginalPrice, decimal DiscountedPrice, string Currency);
