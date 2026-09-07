using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Affiliates;

namespace Tenebit.Application.Affiliates;

/// <summary>
/// Zero PII by construction (spec §6.3/§12.2): this type physically has no field that could carry a
/// customer's name/e-mail/company, so there is nothing for a future change to accidentally start
/// serializing. Never add an OrganizationId or similar here - that data belongs only to
/// admin-facing DTOs.
/// </summary>
public sealed record AffiliateConversionSummaryDto(DateTimeOffset OccurredAt, string Code, string EventType, decimal CommissionAmount, string Currency, bool IsWithinCommissionWindow, bool RequiresReview);

public sealed record AffiliatePayoutSummaryDto(DateTimeOffset MarkedPaidAt, decimal Amount, string Currency, string? PaymentReference);

public sealed record AffiliateDashboardResponse(
    int ActiveCodeCount, int MaxActiveCodeCount, decimal CommissionThisOpenPeriod, decimal TotalAwaitingPayout,
    decimal TotalPaidLifetime, DateTimeOffset? NextPayoutTargetDate, int PayoutDayOfMonth, int PayoutGraceDays);

/// <summary>Read-side aggregation for the affiliate's own dashboard/conversions/payouts pages - every
/// method here is scoped to one affiliate id and never touches another affiliate's rows.</summary>
public sealed class AffiliateDashboardService
{
    private readonly IAffiliateRepository _affiliates;
    private readonly IAffiliateCodeRepository _codes;
    private readonly IAffiliateConversionRepository _conversions;
    private readonly IAffiliatePayoutPeriodRepository _periods;
    private readonly IAffiliatePayoutRepository _payouts;
    private readonly IAffiliateProgramSettingsRepository _settings;
    private readonly IClock _clock;

    public AffiliateDashboardService(
        IAffiliateRepository affiliates, IAffiliateCodeRepository codes, IAffiliateConversionRepository conversions,
        IAffiliatePayoutPeriodRepository periods, IAffiliatePayoutRepository payouts, IAffiliateProgramSettingsRepository settings, IClock clock)
    {
        _affiliates = affiliates;
        _codes = codes;
        _conversions = conversions;
        _periods = periods;
        _payouts = payouts;
        _settings = settings;
        _clock = clock;
    }

    public async Task<Result<AffiliateDashboardResponse>> GetDashboardAsync(Guid affiliateId, CancellationToken cancellationToken)
    {
        var affiliate = await _affiliates.GetByIdAsync(affiliateId, cancellationToken);
        if (affiliate is null) return Result<AffiliateDashboardResponse>.Failure(Error.NotFound("Konto partnerskie nie istnieje."));

        var settings = await _settings.GetAsync(cancellationToken);
        var activeCodes = await _codes.CountActiveByAffiliateAsync(affiliateId, cancellationToken);
        var periods = await _periods.ListByAffiliateAsync(affiliateId, cancellationToken);
        var payouts = await _payouts.ListByAffiliateAsync(affiliateId, cancellationToken);

        var now = _clock.UtcNow;
        var (openStart, _) = AffiliatePayoutPeriod.ResolveMonthBounds(now);
        var openPeriodTotal = periods.FirstOrDefault(p => p.Status == PayoutPeriodStatus.Open && p.PeriodStart == openStart)?.TotalCommission ?? 0m;
        var awaitingPeriods = periods.Where(p => p.Status == PayoutPeriodStatus.AwaitingPayout).ToList();
        var totalAwaiting = awaitingPeriods.Sum(p => p.TotalCommission);
        var totalPaid = payouts.Sum(p => p.Amount);

        DateTimeOffset? nextPayoutDate = null;
        if (awaitingPeriods.Count > 0)
        {
            var earliestClosed = awaitingPeriods.Min(p => p.PeriodEnd);
            var targetMonth = new DateTimeOffset(earliestClosed.Year, earliestClosed.Month, 1, 0, 0, 0, TimeSpan.Zero);
            nextPayoutDate = targetMonth.AddDays(settings.PayoutDayOfMonth - 1);
        }

        return Result<AffiliateDashboardResponse>.Success(new AffiliateDashboardResponse(
            activeCodes, affiliate.ResolveMaxActiveCodes(settings), openPeriodTotal, totalAwaiting, totalPaid,
            nextPayoutDate, settings.PayoutDayOfMonth, settings.PayoutGraceDays));
    }

    public async Task<IReadOnlyList<AffiliateConversionSummaryDto>> GetConversionsAsync(Guid affiliateId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var conversions = await _conversions.ListByAffiliateAsync(affiliateId, page, pageSize, cancellationToken);
        var codes = (await _codes.ListByAffiliateAsync(affiliateId, cancellationToken)).ToDictionary(c => c.Id, c => c.Code);

        return conversions.Select(c => new AffiliateConversionSummaryDto(
            c.OccurredAt, codes.GetValueOrDefault(c.AffiliateCodeId, "?"), c.EventType.ToString(),
            c.CommissionAmount, c.Currency, c.IsWithinCommissionWindow, c.RequiresReview)).ToList();
    }

    public Task<int> CountConversionsAsync(Guid affiliateId, CancellationToken cancellationToken) => _conversions.CountByAffiliateAsync(affiliateId, cancellationToken);

    public async Task<IReadOnlyList<AffiliatePayoutSummaryDto>> GetPayoutsAsync(Guid affiliateId, CancellationToken cancellationToken) =>
        (await _payouts.ListByAffiliateAsync(affiliateId, cancellationToken))
            .Select(p => new AffiliatePayoutSummaryDto(p.MarkedPaidAt, p.Amount, p.Currency, p.PaymentReference))
            .ToList();
}
