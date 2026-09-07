using Microsoft.Extensions.Logging;
using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Common;
using Tenebit.Domain.Subscriptions;

namespace Tenebit.Application.Subscriptions;

public sealed class SubscriptionService
{
    /// <summary>Paddle rejects any chargeable transaction at or below ~0.70 USD
    /// (transaction_balance_less_than_charge_limit - verified against the real sandbox API) - a deep
    /// enough promo code on an already-cheap plan can push the discounted price under that floor. 1.00 EUR
    /// is a safety margin above it (covers FX movement between EUR and the USD-denominated limit) so a
    /// customer gets a clear validation message here instead of a raw Paddle API error inside the checkout
    /// overlay.</summary>
    private const decimal MinimumChargeableAmount = 1.00m;

    private readonly ISubscriptionRepository _subscriptions;
    private readonly IProcessedPaddleEventRepository _processedEvents;
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
    private readonly IAffiliateCodeRepository? _affiliateCodes;
    private readonly IAffiliateClickRepository? _affiliateClicks;

    public SubscriptionService(
        ISubscriptionRepository subscriptions,
        IProcessedPaddleEventRepository processedEvents,
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
        IEmailOutboxWriter? emailOutbox = null,
        IAffiliateCodeRepository? affiliateCodes = null,
        IAffiliateClickRepository? affiliateClicks = null)
    {
        _affiliateCodes = affiliateCodes;
        _affiliateClicks = affiliateClicks;
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
    /// notify about a plan change that happened outside an authenticated request (a Paddle webhook has no
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
    /// plan requires real payment via <see cref="GetCheckoutParamsAsync"/>. Downgrading away from an
    /// active Paddle subscription must go through the Paddle customer portal so cancellation actually
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
            return Result<SubscriptionResponse>.Failure(Error.Validation($"Aby przejść na plan {newPlan.Name}, użyj płatności Paddle (checkout)."));
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
                if (subscription.HasLivePaddleSubscription)
                {
                    return Result<SubscriptionResponse>.Failure(Error.Validation("Ta organizacja ma aktywną płatną subskrypcję. Zarządzaj nią (w tym anulowaniem) w portalu rozliczeń Paddle."));
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

        var discountedPrice = promo.ApplyTo(plan.MonthlyPrice);
        if (discountedPrice < MinimumChargeableAmount)
            return Result<PromoCodeValidationResponse>.Failure(Error.Validation("Ten kod obniża cenę poniżej minimalnej kwoty transakcji akceptowanej przez Paddle. Użyj kodu z mniejszą zniżką."));

        return Result<PromoCodeValidationResponse>.Success(new PromoCodeValidationResponse(
            promo.Code, promo.DiscountType.ToString(), promo.DiscountValue, plan.MonthlyPrice, discountedPrice, plan.Currency,
            promo.DurationType.ToString(), promo.DurationInMonths, promo.Description));
    }

    /// <summary>Looks up, validates and redeems a promo code for the given target plan, shared by both the
    /// first-time checkout and a live upgrade - a null/blank code is a no-op success with no discount.
    /// Redemption (incrementing TimesRedeemed) happens here, before the Paddle call, so a code can't be
    /// spent twice by two concurrent requests racing past a validate-only check.</summary>
    private async Task<Result<PromoCodeDiscount?>> RedeemPromoCodeAsync(SubscriptionPlan targetPlan, string? promoCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(promoCode)) return Result<PromoCodeDiscount?>.Success(null);

        var promo = await _promoCodes.GetByCodeAsync(promoCode, cancellationToken);
        if (promo is null || promo.PlanKey != targetPlan.Key || !promo.IsUsable(_clock.UtcNow))
            return Result<PromoCodeDiscount?>.Failure(Error.Validation("Kod promocyjny jest nieprawidłowy lub wygasł."));

        if (promo.ApplyTo(targetPlan.MonthlyPrice) < MinimumChargeableAmount)
            return Result<PromoCodeDiscount?>.Failure(Error.Validation("Ten kod obniża cenę poniżej minimalnej kwoty transakcji akceptowanej przez Paddle. Użyj kodu z mniejszą zniżką."));

        promo.Redeem();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<PromoCodeDiscount?>.Success(new PromoCodeDiscount(promo.DiscountType, promo.DiscountValue, promo.DurationType, promo.DurationInMonths));
    }

    /// <summary>
    /// Resolves what the frontend needs to open a Paddle.js checkout overlay for the given paid plan -
    /// there is no server-generated hosted redirect URL in Paddle Billing (unlike the old Stripe Checkout
    /// Session), so this returns the Price ID / customer ID / discount ID for Paddle.js to use directly.
    /// </summary>
    public async Task<Result<CheckoutParamsResponse>> GetCheckoutParamsAsync(string planKey, CancellationToken cancellationToken, string? promoCode = null, Guid? attributionToken = null)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner);
        if (access.IsFailure) return Result<CheckoutParamsResponse>.Failure(access.Error!);

        var targetPlan = SubscriptionPlan.FromKey(planKey);
        if (targetPlan is null || targetPlan.Key == SubscriptionPlan.Free.Key)
            return Result<CheckoutParamsResponse>.Failure(Error.Validation($"Unknown plan: {planKey}"));

        if (!_paymentGateway.IsConfigured || !_paymentGateway.IsPlanConfigured(targetPlan.Key))
            return Result<CheckoutParamsResponse>.Failure(Error.Validation("Płatności Paddle nie są jeszcze skonfigurowane dla tego planu."));

        var promoResult = await RedeemPromoCodeAsync(targetPlan, promoCode, cancellationToken);
        if (promoResult.IsFailure) return Result<CheckoutParamsResponse>.Failure(promoResult.Error!);
        var discount = promoResult.Value;

        var organizationId = _currentUser.OrganizationId;
        var subscription = await _subscriptions.GetByOrganizationAsync(organizationId, cancellationToken);
        if (subscription is null)
        {
            subscription = new OrganizationSubscription(organizationId, SubscriptionPlan.Free.Key);
            _subscriptions.Add(subscription);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(subscription.PaddleSubscriptionId))
        {
            var canonical = await _paymentGateway.GetSubscriptionAsync(subscription.PaddleSubscriptionId, cancellationToken)
                ?? throw new PaymentGatewayException("Paddle subscription state could not be verified.");
            if (!string.Equals(canonical.CustomerId, subscription.PaddleCustomerId, StringComparison.Ordinal)
                || (canonical.OrganizationId.HasValue && canonical.OrganizationId.Value != organizationId))
                throw new PaymentGatewayException("Paddle subscription association mismatch.");

            subscription.ReconcileFromPaddle(canonical.PlanKey, canonical.Status, canonical.CurrentPeriodStart, canonical.CurrentPeriodEnd, canonical.SubscriptionId, canonical.CustomerId);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (canonical.Status != SubscriptionStatus.Cancelled)
                return Result<CheckoutParamsResponse>.Failure(Error.Validation("Istnieje subskrypcja Paddle wymagająca naprawy lub zarządzania. Użyj portalu rozliczeniowego zamiast tworzyć drugą subskrypcję."));
        }

        if (string.IsNullOrWhiteSpace(subscription.PaddleCustomerId))
        {
            var customerId = await _paymentGateway.CreateCustomerAsync(
                _currentUser.Email, organizationId, $"tenebit-customer-{organizationId:N}", cancellationToken);
            subscription.AttachPaddleCustomer(customerId);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var affiliateCode = await ResolveAffiliateCodeAsync(attributionToken, cancellationToken);
        var checkoutParams = await _paymentGateway.GetCheckoutParamsAsync(subscription.PaddleCustomerId!, targetPlan.Key, cancellationToken, discount, affiliateCode);
        return Result<CheckoutParamsResponse>.Success(new CheckoutParamsResponse(checkoutParams.PriceId, checkoutParams.CustomerId, checkoutParams.DiscountId, checkoutParams.AffiliateCode));
    }

    /// <summary>Turns the <c>tnb_aff</c> attribution cookie (opaque token, spec §6.2/§13.1) into the
    /// affiliate code it was issued for - never trusting a code the frontend might claim directly. Silent
    /// no-op (returns null) whenever the affiliate program isn't wired up, the token is unrecognized, or
    /// the code has since been deactivated: attribution is a bonus commission source, never something
    /// that can block or fail a checkout.</summary>
    private async Task<string?> ResolveAffiliateCodeAsync(Guid? attributionToken, CancellationToken cancellationToken)
    {
        if (attributionToken is not { } token || _affiliateClicks is null || _affiliateCodes is null) return null;

        var codeId = await _affiliateClicks.FindAffiliateCodeIdByAttributionTokenAsync(token, cancellationToken);
        if (codeId is not { } id) return null;

        var code = await _affiliateCodes.GetByIdAsync(id, cancellationToken);
        return code is { IsActive: true } ? code.Code : null;
    }

    /// <summary>
    /// What ChangePlanAsync would actually do for this target plan, without doing it - lets the
    /// confirmation dialog show a real number (an upgrade's exact proration, computed by Paddle) or a real
    /// date (a downgrade's deferred effective date) instead of the new plan's flat list price, which is
    /// wrong for a mid-cycle switch and is what made a correctly-charged upgrade look like nothing happened.
    /// </summary>
    public async Task<Result<PlanChangePreviewResponse>> PreviewPlanChangeAsync(string planKey, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner);
        if (access.IsFailure) return Result<PlanChangePreviewResponse>.Failure(access.Error!);

        var newPlan = SubscriptionPlan.FromKey(planKey);
        if (newPlan is null || newPlan.Key == SubscriptionPlan.Free.Key)
            return Result<PlanChangePreviewResponse>.Failure(Error.Validation($"Unknown plan: {planKey}"));

        var subscription = await _subscriptions.GetByOrganizationAsync(_currentUser.OrganizationId, cancellationToken);
        if (subscription is null || !subscription.HasLivePaddleSubscription)
            return Result<PlanChangePreviewResponse>.Failure(Error.Validation("Brak aktywnej subskrypcji Paddle do zmiany - najpierw ją załóż przez płatność."));

        var currentPlan = SubscriptionPlan.FromKey(subscription.PlanKey) ?? SubscriptionPlan.Free;
        var timing = newPlan.MonthlyPrice < currentPlan.MonthlyPrice ? PlanChangeTiming.NextBillingPeriod : PlanChangeTiming.Immediately;

        var preview = await _paymentGateway.PreviewPlanChangeAsync(subscription.PaddleSubscriptionId!, newPlan.Key, timing, cancellationToken);
        var chargesNow = timing == PlanChangeTiming.Immediately;
        return Result<PlanChangePreviewResponse>.Success(new PlanChangePreviewResponse(
            preview.AmountDue, preview.Currency, chargesNow, chargesNow ? null : preview.EffectiveAt ?? subscription.CurrentPeriodEnd));
    }

    /// <summary>
    /// Switches an already-live paid subscription directly to a different paid plan. Moving to Free still
    /// goes through the Paddle customer portal, since that's a cancellation, not a price swap.
    ///
    /// An upgrade applies immediately, gated on Paddle actually collecting the prorated payment now (see
    /// PaddlePaymentGateway.ChangeSubscriptionPlanAsync, on_payment_failure=prevent_change). A downgrade
    /// must not take effect - or credit anything - until the current period the org already paid for
    /// actually ends, so Paddle is told to apply it at the next billing period instead: the plan, price and
    /// entitlements stay put until then, no separate schedule object needed (unlike Stripe).
    /// </summary>
    public async Task<Result<SubscriptionResponse>> ChangePlanAsync(string planKey, CancellationToken cancellationToken, string? promoCode = null)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner);
        if (access.IsFailure) return Result<SubscriptionResponse>.Failure(access.Error!);

        var newPlan = SubscriptionPlan.FromKey(planKey);
        if (newPlan is null || newPlan.Key == SubscriptionPlan.Free.Key)
            return Result<SubscriptionResponse>.Failure(Error.Validation($"Unknown plan: {planKey}"));

        if (!_paymentGateway.IsConfigured || !_paymentGateway.IsPlanConfigured(newPlan.Key))
            return Result<SubscriptionResponse>.Failure(Error.Validation("Płatności Paddle nie są jeszcze skonfigurowane dla tego planu."));

        var organizationId = _currentUser.OrganizationId;
        var subscription = await _subscriptions.GetByOrganizationAsync(organizationId, cancellationToken);
        if (subscription is null || !subscription.HasLivePaddleSubscription)
            return Result<SubscriptionResponse>.Failure(Error.Validation("Brak aktywnej subskrypcji Paddle do zmiany - najpierw ją załóż przez płatność."));

        var currentPlan = SubscriptionPlan.FromKey(subscription.PlanKey) ?? SubscriptionPlan.Free;
        decimal? chargedAmount = null;
        string? chargedCurrency = null;

        if (subscription.PlanKey == newPlan.Key)
        {
            // No-op: already on the requested plan.
        }
        else
        {
            var timing = newPlan.MonthlyPrice < currentPlan.MonthlyPrice ? PlanChangeTiming.NextBillingPeriod : PlanChangeTiming.Immediately;
            var promoResult = timing == PlanChangeTiming.Immediately
                ? await RedeemPromoCodeAsync(newPlan, promoCode, cancellationToken)
                : Result<PromoCodeDiscount?>.Success(null); // A deferred downgrade charges nothing today - nothing for a promo code to discount.
            if (promoResult.IsFailure) return Result<SubscriptionResponse>.Failure(promoResult.Error!);
            var discount = promoResult.Value;

            // Salt the idempotency key with the discount shape: a retry that adds/changes the code must not
            // be dropped as a duplicate of an earlier attempt that had no discount.
            var discountKey = discount is null ? "none" : $"{discount.Type}-{discount.Value:0.##}-{discount.DurationType}-{discount.DurationInMonths}";

            PlanChangeResult result;
            try
            {
                result = await _paymentGateway.ChangeSubscriptionPlanAsync(
                    subscription.PaddleSubscriptionId!,
                    newPlan.Key,
                    timing,
                    $"tenebit-planchange-{subscription.PaddleSubscriptionId}-{newPlan.Key}-{discountKey}",
                    cancellationToken,
                    discount);
            }
            catch (PaymentGatewayException ex) when (ex.StatusCode == 402)
            {
                // on_payment_failure=prevent_change (see PaddlePaymentGateway.ChangeSubscriptionPlanAsync)
                // makes Paddle reject the whole update when the proration payment can't be collected - the
                // plan on both Paddle's side and ours is untouched, so this is a normal declined-card
                // outcome, not a system failure.
                return Result<SubscriptionResponse>.Failure(Error.Validation(
                    "Płatność za zmianę planu nie powiodła się. Sprawdź metodę płatności w portalu rozliczeniowym Paddle i spróbuj ponownie."));
            }

            var canonical = result.Subscription;
            if (!string.Equals(canonical.CustomerId, subscription.PaddleCustomerId, StringComparison.Ordinal)
                || (canonical.OrganizationId.HasValue && canonical.OrganizationId.Value != organizationId))
                throw new PaymentGatewayException("Paddle subscription association mismatch.");

            if (timing == PlanChangeTiming.NextBillingPeriod)
            {
                var effectiveAt = result.PendingEffectiveAt ?? subscription.CurrentPeriodEnd;
                subscription.ScheduleDowngrade(newPlan.Key, effectiveAt);

                _activity.Add(new ActivityLog(
                    organizationId,
                    "subscription.plan_change_scheduled",
                    "subscription",
                    subscription.Id,
                    _currentUser.Subject,
                    $"Scheduled downgrade to {newPlan.Name} effective {effectiveAt:O}",
                    _clock.UtcNow));

                await _unitOfWork.SaveChangesAsync(cancellationToken);

                var (scheduledSubject, scheduledHtml) = EmailTemplates.PlanChangeScheduled(
                    _currentUser.Language, newPlan.Name, effectiveAt, _appLinkBuilder.BuildAppUrl("/pricing"));
                await SendPlanChangeEmailAsync(
                    organizationId, _currentUser.Email, _currentUser.Language, scheduledSubject, scheduledHtml,
                    "plan-change-scheduled", $"plan-change-scheduled:{subscription.Id:N}:{effectiveAt:O}:{newPlan.Key}", cancellationToken);
            }
            else
            {
                subscription.ReconcileFromPaddle(canonical.PlanKey, canonical.Status, canonical.CurrentPeriodStart, canonical.CurrentPeriodEnd, canonical.SubscriptionId, canonical.CustomerId);
                chargedAmount = result.AmountCharged;
                chargedCurrency = result.Currency;

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
        }

        var response = await BuildSubscriptionResponseAsync(subscription, cancellationToken);
        return Result<SubscriptionResponse>.Success(response with { LastChargeAmount = chargedAmount, LastChargeCurrency = chargedCurrency });
    }

    /// <summary>Cancels a downgrade scheduled by <see cref="ChangePlanAsync"/> before it takes effect - the
    /// org simply stays on its current plan. No-op safe to call again if nothing is actually pending.</summary>
    public async Task<Result<SubscriptionResponse>> CancelScheduledPlanChangeAsync(CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner);
        if (access.IsFailure) return Result<SubscriptionResponse>.Failure(access.Error!);

        var subscription = await _subscriptions.GetByOrganizationAsync(_currentUser.OrganizationId, cancellationToken);
        if (subscription?.PendingPlanKey is null || string.IsNullOrWhiteSpace(subscription.PaddleSubscriptionId))
            return Result<SubscriptionResponse>.Failure(Error.Validation("Brak zaplanowanej zmiany planu do anulowania."));

        try
        {
            await _paymentGateway.CancelScheduledChangeAsync(subscription.PaddleSubscriptionId, cancellationToken);
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

    /// <summary>Opens the Paddle customer portal so the owner can manage payment method, invoices, or cancel.</summary>
    public async Task<Result<string>> CreateCustomerPortalSessionAsync(CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner);
        if (access.IsFailure) return Result<string>.Failure(access.Error!);

        if (!_paymentGateway.IsConfigured)
        {
            return Result<string>.Failure(Error.Validation("Płatności Paddle nie są jeszcze skonfigurowane."));
        }

        var subscription = await _subscriptions.GetByOrganizationAsync(_currentUser.OrganizationId, cancellationToken);
        if (string.IsNullOrWhiteSpace(subscription?.PaddleCustomerId))
        {
            return Result<string>.Failure(Error.Validation("Organizacja nie ma jeszcze konta rozliczeniowego Paddle."));
        }

        var url = await _paymentGateway.CreateCustomerPortalSessionAsync(subscription.PaddleCustomerId, subscription.PaddleSubscriptionId, cancellationToken);
        return Result<string>.Success(url);
    }

    /// <summary>Handles Paddle's subscription.created/updated/canceled webhooks and syncs our own record.</summary>
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
            return Result.Failure(Error.Validation("Nieprawidłowy webhook Paddle."));
        }

        if (webhookEvent is null) return Result.Success();

        // transaction.completed carries the actual money, not a subscription entitlement change - it is
        // meaningless to this method's SyncFromPaddle/MapStatus logic (Status/PlanKey/CurrentPeriod* are
        // just placeholders on that shape, see PaymentWebhookEvent) and is handled entirely by
        // AffiliateConversionRecordingService.HandleWebhookAsync instead (spec §13.3), invoked separately
        // by the same /subscription/webhook endpoint from the same raw payload.
        if (webhookEvent.EventType == "transaction.completed") return Result.Success();

        // Paddle retries webhook delivery on timeout/5xx - replaying the same notification_id must be a
        // no-op instead of reapplying (and re-logging) the same state change twice (audyt P0.6).
        if (await _processedEvents.ExistsAsync(webhookEvent.EventId, cancellationToken))
        {
            return Result.Success();
        }

        _processedEvents.Add(new ProcessedPaddleEvent(webhookEvent.EventId, _clock.UtcNow));

        var subscription = webhookEvent.OrganizationId.HasValue
            ? await _subscriptions.GetByOrganizationAsync(webhookEvent.OrganizationId.Value, cancellationToken)
            : await _subscriptions.GetByPaddleCustomerAsync(webhookEvent.CustomerId, cancellationToken);

        if (subscription is null)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        // Once a subscription already has a Paddle customer attached (from our own
        // GetCheckoutParamsAsync/CreateCustomerAsync flow), an event whose customer/subscription IDs don't
        // match that record must not be applied to it (audyt AUD3-010).
        var customerMismatch = !string.IsNullOrWhiteSpace(subscription.PaddleCustomerId)
            && !string.Equals(subscription.PaddleCustomerId, webhookEvent.CustomerId, StringComparison.Ordinal);
        var subscriptionMismatch = !string.IsNullOrWhiteSpace(subscription.PaddleSubscriptionId)
            && !string.IsNullOrWhiteSpace(webhookEvent.SubscriptionId)
            && !string.Equals(subscription.PaddleSubscriptionId, webhookEvent.SubscriptionId, StringComparison.Ordinal);
        if (customerMismatch || subscriptionMismatch)
        {
            _activity.Add(new ActivityLog(
                subscription.OrganizationId,
                "subscription.paddle_association_mismatch",
                "subscription",
                subscription.Id,
                "paddle-webhook",
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

        if (webhookEvent.EventType != "subscription.canceled" && !string.IsNullOrWhiteSpace(webhookEvent.SubscriptionId))
        {
            var canonical = await _paymentGateway.GetSubscriptionAsync(webhookEvent.SubscriptionId, cancellationToken)
                ?? throw new PaymentGatewayException("Paddle canonical subscription was not found.");
            if (!string.Equals(canonical.CustomerId, webhookEvent.CustomerId, StringComparison.Ordinal)
                || !string.Equals(canonical.SubscriptionId, webhookEvent.SubscriptionId, StringComparison.Ordinal)
                || (canonical.OrganizationId.HasValue && canonical.OrganizationId != subscription.OrganizationId))
            {
                throw new PaymentGatewayException("Paddle canonical association mismatch.");
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
        subscription.SyncFromPaddle(appliedPlan, appliedStatus, appliedStart, appliedEnd, appliedSubscriptionId, appliedCustomerId, webhookEvent.EventCreatedAt);

        _activity.Add(new ActivityLog(
            subscription.OrganizationId,
            "subscription.paddle_synced",
            "subscription",
            subscription.Id,
            "paddle-webhook",
            $"{webhookEvent.EventType}: {subscription.PlanKey}/{subscription.Status}",
            _clock.UtcNow));

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Two moments deserve the congratulations mail, and neither of them goes through ChangePlanAsync:
        // a first activation (or reactivation) completing via Paddle Checkout, and a plan the org lands on
        // without asking us again right then - above all a scheduled downgrade finally taking effect at the
        // period end, which is exactly the "moved to a smaller plan" moment, but also any switch made
        // straight in Paddle's customer portal. An in-app change already updated PlanKey synchronously
        // before Paddle's echo webhook arrives, so it reads as "no change" here and can't double-send;
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
    DateTimeOffset? PendingPlanEffectiveAt = null,
    /// <summary>Set only on the response to a just-applied plan change - the exact amount Paddle charged
    /// for it (post proration credit; can legitimately be 0). Null everywhere else, including a plain
    /// GetCurrentAsync, where there's no "just happened" charge to report.</summary>
    decimal? LastChargeAmount = null,
    string? LastChargeCurrency = null
);

public sealed record ResourceUsage(string Resource, int Current, int Limit);

public sealed record PromoCodeValidationResponse(
    string Code, string DiscountType, decimal DiscountValue, decimal OriginalPrice, decimal DiscountedPrice, string Currency,
    string DurationType, int? DurationInMonths, string? Description);

/// <summary>What Paddle.js needs to open a checkout overlay - no secrets, safe to hand to the frontend.</summary>
public sealed record CheckoutParamsResponse(string PriceId, string CustomerId, string? DiscountId, string? AffiliateCode = null);

/// <summary>What a plan switch would actually do right now: either the exact amount Paddle would charge
/// immediately (an upgrade), or - when ChargesNow is false - the date the new price takes effect for free
/// with nothing charged today (a downgrade).</summary>
public sealed record PlanChangePreviewResponse(decimal AmountDue, string Currency, bool ChargesNow, DateTimeOffset? EffectiveAt);
