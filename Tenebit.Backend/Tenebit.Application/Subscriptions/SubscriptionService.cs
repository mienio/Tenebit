using Microsoft.Extensions.Logging;
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
    private readonly IOrganizationRepository _organizations;
    private readonly IOrganizationUserRepository _organizationUsers;
    private readonly IEmailSender _emailSender;
    private readonly IEmailOutboxWriter? _emailOutbox;
    private readonly ILogger<SubscriptionService> _logger;

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
        IPromoCodeRepository promoCodes,
        IOrganizationRepository organizations,
        IOrganizationUserRepository organizationUsers,
        IEmailSender emailSender,
        ILogger<SubscriptionService> logger,
        IEmailOutboxWriter? emailOutbox = null)
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
        _organizations = organizations;
        _organizationUsers = organizationUsers;
        _emailSender = emailSender;
        _logger = logger;
        _emailOutbox = emailOutbox;
    }

    /// <summary>Best-effort delivery for the "nice to receive" plan-change emails (congratulations, or a
    /// scheduled-downgrade notice) - through the outbox when available (retried on transient failure), a
    /// direct send otherwise. Never lets an email problem fail the plan change itself.</summary>
    private async Task SendPlanChangeEmailAsync(Guid organizationId, string recipient, string language, string subject, string html, string purpose, string idempotencyKey, CancellationToken cancellationToken)
    {
        try
        {
            if (_emailOutbox is not null)
            {
                await _emailOutbox.EnqueueAsync(organizationId, recipient, subject, html, purpose, idempotencyKey, cancellationToken);
            }
            else
            {
                await _emailSender.SendAsync(recipient, subject, html, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nie udało się wysłać e-maila o zmianie planu ({Purpose}) dla organizacji {OrganizationId}", purpose, organizationId);
        }
    }

    /// <summary>Every current org owner's email + the org's own language preference - who and how to
    /// notify about a plan change that happened outside an authenticated request (a Stripe webhook has no
    /// <see cref="ICurrentUser"/> to address).</summary>
    private async Task<(string Language, IReadOnlyList<string> OwnerEmails)> GetOrganizationOwnersAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var organization = await _organizations.GetAsync(organizationId, cancellationToken);
        var users = await _organizationUsers.ListAsync(organizationId, cancellationToken);
        var owners = users
            .Where(u => u.Roles.Any(r => r.Role == TenebitRoles.Owner) && !string.IsNullOrWhiteSpace(u.Email))
            .Select(u => u.Email)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return (organization?.Language ?? "pl", owners);
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

        return Result<SubscriptionResponse>.Success(await BuildSubscriptionResponseAsync(subscription, cancellationToken));
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

            return Result<SubscriptionResponse>.Success(await BuildSubscriptionResponseAsync(subscription, cancellationToken));
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

    /// <summary>Looks up, validates and redeems a promo code for the given target plan, shared by both the
    /// first-time checkout and a live upgrade - a null/blank code is a no-op success with no discount.
    /// Redemption (incrementing TimesRedeemed) happens here, before the Stripe call, so a code can't be
    /// spent twice by two concurrent requests racing past a validate-only check.</summary>
    private async Task<Result<PromoCodeDiscount?>> RedeemPromoCodeAsync(SubscriptionPlan targetPlan, string? promoCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(promoCode)) return Result<PromoCodeDiscount?>.Success(null);

        var promo = await _promoCodes.GetByCodeAsync(promoCode, cancellationToken);
        if (promo is null || promo.PlanKey != targetPlan.Key || !promo.IsUsable(_clock.UtcNow))
            return Result<PromoCodeDiscount?>.Failure(Error.Validation("Kod promocyjny jest nieprawidłowy lub wygasł."));

        promo.Redeem();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<PromoCodeDiscount?>.Success(new PromoCodeDiscount(promo.DiscountType, promo.DiscountValue));
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

        var promoResult = await RedeemPromoCodeAsync(targetPlan, promoCode, cancellationToken);
        if (promoResult.IsFailure) return Result<string>.Failure(promoResult.Error!);
        var discount = promoResult.Value;

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

        // GetOrCreateCheckoutAttempt reuses attemptId (and so this Idempotency-Key) for up to 30 minutes,
        // to dedupe genuine double-clicks into one Stripe call. But Stripe ties an Idempotency-Key to the
        // exact request body it first saw - if the plan or promo code differs between retries within that
        // window (e.g. no code, then the same checkout retried with a promo code), reusing the key sends a
        // *different* body under the same key and Stripe rejects it outright. Salting the key with the plan
        // and discount shape keeps real duplicate retries deduped while giving a differently-shaped retry
        // its own key.
        var discountKey = discount is null ? "none" : $"{discount.Type}-{discount.Value:0.##}";
        var checkoutUrl = await _paymentGateway.CreateCheckoutSessionAsync(
            subscription.StripeCustomerId!,
            organizationId,
            targetPlan.Key,
            _appLinkBuilder.BuildAppUrl(successPath),
            _appLinkBuilder.BuildAppUrl(cancelPath),
            $"tenebit-checkout-{attemptId:N}-{targetPlan.Key}-{discountKey}",
            cancellationToken,
            discount);
        return Result<string>.Success(checkoutUrl);
    }

    /// <summary>
    /// Switches an already-live paid subscription directly to a different paid plan, without a new
    /// Checkout Session (CreateCheckoutSessionAsync refuses to create a second one on purpose). Moving to
    /// Free still goes through the Billing Portal, since that's a cancellation, not a price swap.
    ///
    /// An upgrade applies immediately, gated on Stripe actually collecting the prorated payment now (see
    /// StripePaymentGateway.ChangeSubscriptionPlanAsync). A downgrade must not take effect - or credit
    /// anything - until the current period the org already paid for actually ends, so it's scheduled on
    /// Stripe's side instead (StripePaymentGateway.ScheduleDowngradeAsync): the plan, price and entitlements
    /// stay put until then.
    /// </summary>
    public async Task<Result<SubscriptionResponse>> ChangePlanAsync(string planKey, CancellationToken cancellationToken, string? promoCode = null)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner);
        if (access.IsFailure) return Result<SubscriptionResponse>.Failure(access.Error!);

        var newPlan = SubscriptionPlan.FromKey(planKey);
        if (newPlan is null || newPlan.Key == SubscriptionPlan.Free.Key)
            return Result<SubscriptionResponse>.Failure(Error.Validation($"Unknown plan: {planKey}"));

        if (!_paymentGateway.IsConfigured || !_paymentGateway.IsPlanConfigured(newPlan.Key))
            return Result<SubscriptionResponse>.Failure(Error.Validation("Płatności Stripe nie są jeszcze skonfigurowane dla tego planu."));

        var organizationId = _currentUser.OrganizationId;
        var subscription = await _subscriptions.GetByOrganizationAsync(organizationId, cancellationToken);
        if (subscription is null || !subscription.HasLiveStripeSubscription)
            return Result<SubscriptionResponse>.Failure(Error.Validation("Brak aktywnej subskrypcji Stripe do zmiany - najpierw ją załóż przez płatność."));

        var currentPlan = SubscriptionPlan.FromKey(subscription.PlanKey) ?? SubscriptionPlan.Free;

        if (subscription.PlanKey == newPlan.Key)
        {
            // No-op: already on the requested plan.
        }
        else if (newPlan.MonthlyPrice < currentPlan.MonthlyPrice)
        {
            PaymentScheduleState schedule;
            try
            {
                schedule = await _paymentGateway.ScheduleDowngradeAsync(
                    subscription.StripeSubscriptionId!,
                    subscription.StripeScheduleId,
                    newPlan.Key,
                    $"tenebit-scheduledowngrade-{subscription.StripeSubscriptionId}-{newPlan.Key}",
                    cancellationToken);
            }
            catch (PaymentGatewayException ex) when (ex.StatusCode is >= 400 and < 500)
            {
                return Result<SubscriptionResponse>.Failure(Error.Validation(
                    "Nie udało się zaplanować zmiany planu. Spróbuj ponownie później."));
            }

            subscription.ScheduleDowngrade(schedule.PendingPlanKey, schedule.EffectiveAt, schedule.ScheduleId);

            _activity.Add(new ActivityLog(
                organizationId,
                "subscription.plan_change_scheduled",
                "subscription",
                subscription.Id,
                _currentUser.Subject,
                $"Scheduled downgrade to {newPlan.Name} effective {schedule.EffectiveAt:O}",
                _clock.UtcNow));

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var (scheduledSubject, scheduledHtml) = EmailTemplates.PlanChangeScheduled(
                _currentUser.Language, newPlan.Name, schedule.EffectiveAt, _appLinkBuilder.BuildAppUrl("/pricing"));
            await SendPlanChangeEmailAsync(
                organizationId, _currentUser.Email, _currentUser.Language, scheduledSubject, scheduledHtml,
                "plan-change-scheduled", $"plan-change-scheduled:{subscription.Id:N}:{schedule.ScheduleId}:{newPlan.Key}", cancellationToken);
        }
        else
        {
            var promoResult = await RedeemPromoCodeAsync(newPlan, promoCode, cancellationToken);
            if (promoResult.IsFailure) return Result<SubscriptionResponse>.Failure(promoResult.Error!);
            var discount = promoResult.Value;

            // Salt the idempotency key with the discount shape (mirrors CreateCheckoutSessionAsync): a
            // retry that adds/changes the code must not be dropped as a duplicate of an earlier attempt
            // that had no discount, or Stripe would reject the differently-shaped body under the same key.
            var discountKey = discount is null ? "none" : $"{discount.Type}-{discount.Value:0.##}";

            PaymentSubscriptionState canonical;
            try
            {
                canonical = await _paymentGateway.ChangeSubscriptionPlanAsync(
                    subscription.StripeSubscriptionId!,
                    newPlan.Key,
                    $"tenebit-planchange-{subscription.StripeSubscriptionId}-{newPlan.Key}-{discountKey}",
                    cancellationToken,
                    discount);
            }
            catch (PaymentGatewayException ex) when (ex.StatusCode == 402)
            {
                // error_if_incomplete (see StripePaymentGateway.ChangeSubscriptionPlanAsync) makes Stripe
                // reject the whole update when the proration invoice can't be paid - the plan on both
                // Stripe's side and ours is untouched, so this is a normal declined-card outcome, not a
                // system failure.
                return Result<SubscriptionResponse>.Failure(Error.Validation(
                    "Płatność za zmianę planu nie powiodła się. Sprawdź metodę płatności w portalu rozliczeniowym Stripe i spróbuj ponownie."));
            }

            if (!string.Equals(canonical.CustomerId, subscription.StripeCustomerId, StringComparison.Ordinal)
                || (canonical.OrganizationId.HasValue && canonical.OrganizationId.Value != organizationId))
                throw new PaymentGatewayException("Stripe subscription association mismatch.");

            subscription.ReconcileFromStripe(canonical.PlanKey, canonical.Status, canonical.CurrentPeriodStart, canonical.CurrentPeriodEnd, canonical.SubscriptionId, canonical.CustomerId);

            _activity.Add(new ActivityLog(
                organizationId,
                "subscription.plan_changed",
                "subscription",
                subscription.Id,
                _currentUser.Subject,
                $"Changed to {newPlan.Name}",
                _clock.UtcNow));

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var (changedSubject, changedHtml) = EmailTemplates.PlanChanged(
                _currentUser.Language, newPlan.Name, _appLinkBuilder.BuildAppUrl("/dashboard"));
            await SendPlanChangeEmailAsync(
                organizationId, _currentUser.Email, _currentUser.Language, changedSubject, changedHtml,
                "plan-changed", $"plan-changed:{subscription.Id:N}:{canonical.SubscriptionId}:{newPlan.Key}:{canonical.CurrentPeriodStart:O}", cancellationToken);
        }

        return Result<SubscriptionResponse>.Success(await BuildSubscriptionResponseAsync(subscription, cancellationToken));
    }

    /// <summary>Cancels a downgrade scheduled by <see cref="ChangePlanAsync"/> before it takes effect - the
    /// org simply stays on its current plan. No-op safe to call again if nothing is actually pending.</summary>
    public async Task<Result<SubscriptionResponse>> CancelScheduledPlanChangeAsync(CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner);
        if (access.IsFailure) return Result<SubscriptionResponse>.Failure(access.Error!);

        var subscription = await _subscriptions.GetByOrganizationAsync(_currentUser.OrganizationId, cancellationToken);
        if (subscription?.StripeScheduleId is not { } scheduleId)
            return Result<SubscriptionResponse>.Failure(Error.Validation("Brak zaplanowanej zmiany planu do anulowania."));

        try
        {
            await _paymentGateway.ReleaseScheduleAsync(scheduleId, cancellationToken);
        }
        catch (PaymentGatewayException)
        {
            return Result<SubscriptionResponse>.Failure(Error.Validation("Nie udało się anulować zaplanowanej zmiany planu. Spróbuj ponownie później."));
        }

        subscription.ClearPendingPlanChange();

        _activity.Add(new ActivityLog(
            subscription.OrganizationId,
            "subscription.plan_change_cancelled",
            "subscription",
            subscription.Id,
            _currentUser.Subject,
            "Cancelled scheduled plan change",
            _clock.UtcNow));

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<SubscriptionResponse>.Success(await BuildSubscriptionResponseAsync(subscription, cancellationToken));
    }

    private async Task<SubscriptionResponse> BuildSubscriptionResponseAsync(OrganizationSubscription subscription, CancellationToken cancellationToken)
    {
        var plan = SubscriptionPlan.FromKey(subscription.PlanKey) ?? SubscriptionPlan.Free;
        var pendingPlan = subscription.PendingPlanKey is null ? null : SubscriptionPlan.FromKey(subscription.PendingPlanKey);
        var usage = await BuildUsageAsync(subscription, cancellationToken);

        return new SubscriptionResponse(
            subscription.Id,
            subscription.PlanKey,
            plan.Name,
            plan.AssetLimit,
            plan.MonthlyPrice,
            plan.Currency,
            usage.First(x => x.Resource == "assets").Current,
            subscription.Status.ToString(),
            subscription.CurrentPeriodEnd,
            usage,
            pendingPlan?.Key,
            pendingPlan?.Name,
            subscription.PendingPlanEffectiveAt
        );
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

        var planBefore = subscription.PlanKey;
        var wasEntitledBefore = subscription.IsEntitledToPaidPlan;
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

        // Two moments deserve the congratulations mail, and neither of them goes through ChangePlanAsync:
        // a first activation (or reactivation) completing via Stripe Checkout, and a plan the org lands on
        // without asking us again right then - above all a scheduled downgrade finally taking effect at the
        // period end, which is exactly the "moved to a smaller plan" moment, but also any switch made
        // straight in Stripe's billing portal. An in-app change already updated PlanKey synchronously
        // before Stripe's echo webhook arrives, so it reads as "no change" here and can't double-send;
        // the outbox's idempotency key is the second line of defence for that race.
        var becameEntitled = !wasEntitledBefore && subscription.IsEntitledToPaidPlan;
        var switchedPaidPlan = wasEntitledBefore && subscription.IsEntitledToPaidPlan && subscription.PlanKey != planBefore;
        if (becameEntitled || switchedPaidPlan)
        {
            var plan = SubscriptionPlan.FromKey(subscription.PlanKey) ?? SubscriptionPlan.Free;
            var (language, ownerEmails) = await GetOrganizationOwnersAsync(subscription.OrganizationId, cancellationToken);
            foreach (var ownerEmail in ownerEmails)
            {
                var (subject, html) = EmailTemplates.PlanChanged(language, plan.Name, _appLinkBuilder.BuildAppUrl("/dashboard"));
                await SendPlanChangeEmailAsync(
                    subscription.OrganizationId, ownerEmail, language, subject, html,
                    "plan-changed", $"plan-changed:{subscription.Id:N}:{appliedSubscriptionId}:{plan.Key}:{appliedStart:O}", cancellationToken);
            }
        }

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
    IReadOnlyList<ResourceUsage> Usage,
    string? PendingPlanKey = null,
    string? PendingPlanName = null,
    DateTimeOffset? PendingPlanEffectiveAt = null
);

public sealed record ResourceUsage(string Resource, int Current, int Limit);

public sealed record PromoCodeValidationResponse(
    string Code, string DiscountType, decimal DiscountValue, decimal OriginalPrice, decimal DiscountedPrice, string Currency);
