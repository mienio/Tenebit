using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Affiliates;

namespace Tenebit.Application.Affiliates;

/// <summary>
/// The one place an <see cref="AffiliateConversion"/> is ever created (spec §3.4/§6.2/§13.3).
/// <see cref="HandleWebhookAsync"/> is the only caller of <see cref="RecordConversionAsync"/> in
/// production - exactly like <see cref="Tenebit.Domain.Subscriptions.ProcessedPaddleEvent"/> guards the
/// main subscription sync against double-processing, and is itself invoked from the very same
/// <c>/subscription/webhook</c> POST as <see cref="Tenebit.Application.Subscriptions.SubscriptionService.HandleWebhookAsync"/>
/// (Paddle delivers every event type to one configured URL) - it independently re-parses the raw
/// payload via <see cref="IPaymentGateway.ParseWebhookEvent"/> and simply ignores anything that isn't
/// <c>transaction.completed</c>, keeping the Affiliates and Subscriptions domains decoupled from each
/// other rather than threading this through SubscriptionService itself.
/// </summary>
public sealed class AffiliateConversionRecordingService
{
    private readonly IAffiliateCodeRepository _codes;
    private readonly IAffiliateRepository _affiliates;
    private readonly IAffiliateConversionRepository _conversions;
    private readonly IAffiliatePayoutPeriodRepository _periods;
    private readonly IAffiliateProgramSettingsRepository _settings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IPaymentGateway _paymentGateway;
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IOrganizationUserRepository _organizationUsers;

    public AffiliateConversionRecordingService(
        IAffiliateCodeRepository codes, IAffiliateRepository affiliates, IAffiliateConversionRepository conversions,
        IAffiliatePayoutPeriodRepository periods, IAffiliateProgramSettingsRepository settings, IUnitOfWork unitOfWork, IClock clock,
        IPaymentGateway paymentGateway, ISubscriptionRepository subscriptions, IOrganizationUserRepository organizationUsers)
    {
        _codes = codes;
        _affiliates = affiliates;
        _conversions = conversions;
        _periods = periods;
        _settings = settings;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _paymentGateway = paymentGateway;
        _subscriptions = subscriptions;
        _organizationUsers = organizationUsers;
    }

    /// <summary>
    /// The Paddle webhook entry point (spec §13.3). Re-parses the same raw payload
    /// <see cref="Tenebit.Application.Subscriptions.SubscriptionService.HandleWebhookAsync"/> already
    /// verified the signature of - a second HMAC/JSON pass is cheap and keeps this service from needing
    /// to know anything about that one's <c>ProcessedPaddleEvent</c> idempotency bookkeeping. Silently a
    /// no-op for every event type except <c>transaction.completed</c>, for a transaction with no
    /// affiliate attribution (no <c>custom_data.affiliate_code</c> - the overwhelmingly common case), or
    /// for a customer Tenebit has no subscription record for.
    /// </summary>
    public async Task<Result> HandleWebhookAsync(string payload, string signatureHeader, CancellationToken cancellationToken)
    {
        PaymentWebhookEvent? webhookEvent;
        try
        {
            webhookEvent = _paymentGateway.ParseWebhookEvent(payload, signatureHeader);
        }
        catch (PaymentWebhookValidationException)
        {
            // Already surfaced as a rejected webhook by SubscriptionService.HandleWebhookAsync on the same
            // request - nothing further to report here.
            return Result.Success();
        }

        if (webhookEvent is null || webhookEvent.EventType != "transaction.completed") return Result.Success();
        if (string.IsNullOrWhiteSpace(webhookEvent.AffiliateCode) || string.IsNullOrWhiteSpace(webhookEvent.TransactionId))
            return Result.Success();

        var subscription = await _subscriptions.GetByPaddleCustomerAsync(webhookEvent.CustomerId, cancellationToken);
        if (subscription is null) return Result.Success();

        var buyerEmail = (await _organizationUsers.ListAsync(subscription.OrganizationId, cancellationToken))
            .Where(u => u.Roles.Any(r => r.Role == TenebitRoles.Owner) && !string.IsNullOrWhiteSpace(u.Email))
            .Select(u => u.Email)
            .FirstOrDefault();

        return await RecordConversionAsync(
            webhookEvent.AffiliateCode, subscription.OrganizationId, subscription.Id, webhookEvent.TransactionId,
            webhookEvent.IsRenewal ? AffiliateConversionEventType.Renewal : AffiliateConversionEventType.InitialSale,
            webhookEvent.EventCreatedAt, webhookEvent.GrossAmount, webhookEvent.NetAmount, webhookEvent.Currency ?? "EUR",
            buyerEmail, cancellationToken);
    }

    public async Task<Result> RecordConversionAsync(
        string affiliateCode, Guid organizationId, Guid organizationSubscriptionId, string paddleTransactionId,
        AffiliateConversionEventType eventType, DateTimeOffset occurredAt, decimal grossAmount, decimal netAmount,
        string currency, string? buyerEmail, CancellationToken cancellationToken)
    {
        // Idempotency first, before anything else touches the database - a retried webhook delivery
        // must be a true no-op.
        if (await _conversions.ExistsByPaddleTransactionAsync(paddleTransactionId, cancellationToken))
        {
            return Result.Success();
        }

        var code = await _codes.GetByCodeAsync(affiliateCode, cancellationToken);
        if (code is null || !code.IsActive)
        {
            // The code stopped existing/being active between checkout and webhook delivery - never
            // trust custom_data alone (spec §6.2 pkt 3): silently skip, this is not an error state for
            // the webhook caller.
            return Result.Success();
        }

        var affiliate = await _affiliates.GetByIdAsync(code.AffiliateId, cancellationToken);
        if (affiliate is null || affiliate.Status == AffiliateStatus.Blocked)
        {
            return Result.Success();
        }

        var settings = await _settings.GetAsync(cancellationToken);
        var commissionPercent = affiliate.ResolveCommissionPercent(settings);

        var firstSaleDate = await _conversions.GetFirstSaleDateAsync(code.Id, organizationId, cancellationToken);
        var isWithinWindow = eventType == AffiliateConversionEventType.InitialSale || firstSaleDate is null
            ? true
            : settings.DefaultCommissionWindowMonths is not { } windowMonths || occurredAt < firstSaleDate.Value.AddMonths(windowMonths);

        var requiresReview = IsLikelySelfReferral(affiliate.Email, buyerEmail);

        var conversion = AffiliateConversion.Create(
            affiliate.Id, code.Id, organizationId, organizationSubscriptionId, paddleTransactionId, eventType,
            occurredAt, grossAmount, netAmount, currency, settings.CommissionBase, commissionPercent,
            isWithinWindow, requiresReview, requiresReview ? "E-mail kupującego pokrywa się z e-mailem afilianta." : null);

        _conversions.Add(conversion);

        if (!requiresReview && isWithinWindow)
        {
            var (periodStart, periodEnd) = AffiliatePayoutPeriod.ResolveMonthBounds(occurredAt);
            var period = await _periods.GetOpenForMonthAsync(affiliate.Id, periodStart, cancellationToken);
            if (period is null)
            {
                period = AffiliatePayoutPeriod.OpenFor(affiliate.Id, occurredAt, _clock.UtcNow);
                _periods.Add(period);
            }

            period.AddCommission(conversion.CommissionAmount);
            conversion.AssignToPeriod(period.Id);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>Heuristic only (spec §6.4/§12.7): exact-match or same-domain e-mail. Never blocks the
    /// conversion from being recorded - it only routes it to admin review instead of automatic payout
    /// aggregation.</summary>
    private static bool IsLikelySelfReferral(string affiliateEmail, string? buyerEmail)
    {
        if (string.IsNullOrWhiteSpace(buyerEmail)) return false;
        if (string.Equals(affiliateEmail, buyerEmail, StringComparison.OrdinalIgnoreCase)) return true;

        var affiliateDomain = affiliateEmail.Split('@').ElementAtOrDefault(1);
        var buyerDomain = buyerEmail.Split('@').ElementAtOrDefault(1);
        return !string.IsNullOrEmpty(affiliateDomain) && string.Equals(affiliateDomain, buyerDomain, StringComparison.OrdinalIgnoreCase);
    }
}
