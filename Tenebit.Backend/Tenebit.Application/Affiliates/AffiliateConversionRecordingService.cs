using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Affiliates;

namespace Tenebit.Application.Affiliates;

/// <summary>
/// The one place an <see cref="AffiliateConversion"/> is ever created (spec §3.4/§6.2/§13.3) - the
/// Paddle webhook handler is the only caller, exactly like <see cref="Tenebit.Domain.Subscriptions.ProcessedPaddleEvent"/>
/// guards the main subscription sync against double-processing. Not wired to a live Paddle event yet
/// in this phase (see the Faza 2+ item in PLAN.md - <c>transaction.completed</c> isn't parsed by
/// <c>IPaymentGateway</c> today and doing that safely needs verification against a real Paddle payload
/// first) but built and tested now so that wiring is the only thing left to do later.
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

    public AffiliateConversionRecordingService(
        IAffiliateCodeRepository codes, IAffiliateRepository affiliates, IAffiliateConversionRepository conversions,
        IAffiliatePayoutPeriodRepository periods, IAffiliateProgramSettingsRepository settings, IUnitOfWork unitOfWork, IClock clock)
    {
        _codes = codes;
        _affiliates = affiliates;
        _conversions = conversions;
        _periods = periods;
        _settings = settings;
        _unitOfWork = unitOfWork;
        _clock = clock;
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
