using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Affiliates;
using Tenebit.Domain.Identity;

namespace Tenebit.Application.Admin;

public sealed record AffiliateAdminDashboardSummary(int PendingApprovalCount, int AwaitingPayoutAffiliateCount, int UnreadMessageThreadCount);

public sealed record AffiliateAdminListItem(
    Guid Id, string FullName, string Email, string Status, int ActiveCodeCount, int LifetimeConversionCount,
    decimal TotalDueNow, decimal TotalPaidLifetime, DateTimeOffset CreatedAt);

public sealed record AffiliateAdminCodeItem(Guid Id, string Code, string? CountryCode, bool IsActive, int ClickCount, DateTimeOffset CreatedAt);

/// <summary>Admin-only view of a conversion - unlike <c>AffiliateConversionSummaryDto</c> this
/// deliberately carries <see cref="OrganizationId"/>, because the admin already has full authority
/// over every organization in the platform (spec §9.2).</summary>
public sealed record AffiliateAdminConversionItem(
    Guid Id, DateTimeOffset OccurredAt, Guid? OrganizationId, string Code, string EventType,
    decimal GrossAmount, decimal NetAmount, decimal CommissionAmount, string Currency, bool RequiresReview, bool IsWithinCommissionWindow);

public sealed record AffiliateAdminPayoutPeriodItem(Guid Id, DateTimeOffset PeriodStart, DateTimeOffset PeriodEnd, decimal TotalCommission, string Status);
public sealed record AffiliateAdminPayoutItem(Guid Id, decimal Amount, string Currency, DateTimeOffset MarkedPaidAt, string? PaymentReference, string? Note, IReadOnlyList<Guid> CoveredPeriodIds);

public sealed record AffiliateAdminDetail(
    Guid Id, string FirstName, string LastName, string Email, string Status, string? CountryCode, string? PhoneNumber,
    string? CompanyName, string? TaxId, string? RevolutTag, decimal? CommissionPercentOverride, int? MaxActiveCodesOverride,
    decimal ResolvedCommissionPercent, int ResolvedMaxActiveCodes, DateTimeOffset CreatedAt, DateTimeOffset? ApprovedAt,
    DateTimeOffset? BlockedAt, string? BlockedReason, IReadOnlyList<AffiliateAdminCodeItem> Codes,
    IReadOnlyList<AffiliateAdminConversionItem> Conversions, IReadOnlyList<AffiliateAdminPayoutPeriodItem> PayoutPeriods,
    IReadOnlyList<AffiliateAdminPayoutItem> Payouts);

/// <summary>
/// Everything the admin does to a partner account (spec §9): approve/block, override commission/code
/// limit, drill down into who paid them when, and confirm a Revolut transfer really happened. Every
/// mutating action here writes an <see cref="AdminAuditLog"/> entry - the platform has exactly one
/// admin account, so unlike a multi-admin system there is no separate "who" to record beyond the
/// action, target and IP (same convention as the rest of AdminEndpoints).
/// </summary>
public sealed class AffiliateAdminService
{
    private readonly IAffiliateRepository _affiliates;
    private readonly IAffiliateCodeRepository _codes;
    private readonly IAffiliateConversionRepository _conversions;
    private readonly IAffiliatePayoutPeriodRepository _periods;
    private readonly IAffiliatePayoutRepository _payouts;
    private readonly IAffiliateProgramSettingsRepository _settings;
    private readonly IAffiliateMessageThreadRepository _messageThreads;
    private readonly IAdminRepository _admin;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public AffiliateAdminService(
        IAffiliateRepository affiliates, IAffiliateCodeRepository codes, IAffiliateConversionRepository conversions,
        IAffiliatePayoutPeriodRepository periods, IAffiliatePayoutRepository payouts, IAffiliateProgramSettingsRepository settings,
        IAffiliateMessageThreadRepository messageThreads, IAdminRepository admin, IUnitOfWork unitOfWork, IClock clock)
    {
        _affiliates = affiliates;
        _codes = codes;
        _conversions = conversions;
        _periods = periods;
        _payouts = payouts;
        _settings = settings;
        _messageThreads = messageThreads;
        _admin = admin;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<AffiliateAdminDashboardSummary> GetDashboardSummaryAsync(CancellationToken cancellationToken)
    {
        var pending = await _affiliates.ListAsync(AffiliateStatus.PendingApproval, cancellationToken);
        var allAffiliates = await _affiliates.ListAsync(null, cancellationToken);
        var awaitingCount = 0;
        foreach (var affiliate in allAffiliates)
        {
            var awaiting = await _periods.ListAwaitingPayoutAsync(affiliate.Id, cancellationToken);
            if (awaiting.Count > 0) awaitingCount++;
        }
        var unreadMessages = await _messageThreads.CountUnreadByAdminAsync(cancellationToken);
        return new AffiliateAdminDashboardSummary(pending.Count, awaitingCount, unreadMessages);
    }

    public async Task<IReadOnlyList<AffiliateAdminListItem>> ListAsync(AffiliateStatus? status, CancellationToken cancellationToken)
    {
        var affiliates = await _affiliates.ListAsync(status, cancellationToken);
        var items = new List<AffiliateAdminListItem>(affiliates.Count);
        foreach (var affiliate in affiliates)
        {
            var activeCodes = await _codes.CountActiveByAffiliateAsync(affiliate.Id, cancellationToken);
            var conversionCount = await _conversions.CountByAffiliateAsync(affiliate.Id, cancellationToken);
            var awaitingPeriods = await _periods.ListAwaitingPayoutAsync(affiliate.Id, cancellationToken);
            var payouts = await _payouts.ListByAffiliateAsync(affiliate.Id, cancellationToken);
            items.Add(new AffiliateAdminListItem(
                affiliate.Id, affiliate.FullName, affiliate.Email, affiliate.Status.ToString(), activeCodes,
                conversionCount, awaitingPeriods.Sum(p => p.TotalCommission), payouts.Sum(p => p.Amount), affiliate.CreatedAt));
        }
        return items;
    }

    public async Task<Result<AffiliateAdminDetail>> GetDetailAsync(Guid affiliateId, CancellationToken cancellationToken)
    {
        var affiliate = await _affiliates.GetByIdAsync(affiliateId, cancellationToken);
        if (affiliate is null) return Result<AffiliateAdminDetail>.Failure(Error.NotFound("Afiliant nie istnieje."));

        var settings = await _settings.GetAsync(cancellationToken);
        var codes = await _codes.ListByAffiliateAsync(affiliateId, cancellationToken);
        var codeNames = codes.ToDictionary(c => c.Id, c => c.Code);
        var conversions = await _conversions.ListByAffiliateAsync(affiliateId, 1, 500, cancellationToken);
        var periods = await _periods.ListByAffiliateAsync(affiliateId, cancellationToken);
        var payouts = await _payouts.ListByAffiliateAsync(affiliateId, cancellationToken);

        return Result<AffiliateAdminDetail>.Success(new AffiliateAdminDetail(
            affiliate.Id, affiliate.FirstName, affiliate.LastName, affiliate.Email, affiliate.Status.ToString(),
            affiliate.CountryCode, affiliate.PhoneNumber, affiliate.CompanyName, affiliate.TaxId, affiliate.RevolutTag,
            affiliate.CommissionPercentOverride, affiliate.MaxActiveCodesOverride,
            affiliate.ResolveCommissionPercent(settings), affiliate.ResolveMaxActiveCodes(settings),
            affiliate.CreatedAt, affiliate.ApprovedAt, affiliate.BlockedAt, affiliate.BlockedReason,
            codes.Select(c => new AffiliateAdminCodeItem(c.Id, c.Code, c.CountryCode, c.IsActive, c.ClickCount, c.CreatedAt)).ToList(),
            conversions.Select(c => new AffiliateAdminConversionItem(
                c.Id, c.OccurredAt, c.OrganizationId, codeNames.GetValueOrDefault(c.AffiliateCodeId, "?"), c.EventType.ToString(),
                c.GrossAmount, c.NetAmount, c.CommissionAmount, c.Currency, c.RequiresReview, c.IsWithinCommissionWindow)).ToList(),
            periods.Select(p => new AffiliateAdminPayoutPeriodItem(p.Id, p.PeriodStart, p.PeriodEnd, p.TotalCommission, p.Status.ToString())).ToList(),
            payouts.Select(p => new AffiliateAdminPayoutItem(p.Id, p.Amount, p.Currency, p.MarkedPaidAt, p.PaymentReference, p.Note, p.CoveredPeriodIds)).ToList()));
    }

    public async Task<Result> ApproveAsync(Guid affiliateId, string? actorIp, CancellationToken cancellationToken)
    {
        var affiliate = await _affiliates.GetByIdAsync(affiliateId, cancellationToken);
        if (affiliate is null) return Result.Failure(Error.NotFound("Afiliant nie istnieje."));

        try { affiliate.Approve(_clock.UtcNow); }
        catch (Domain.Common.DomainException ex) { return Result.Failure(Error.Validation(ex.Message)); }

        _admin.AddAdminAudit(new AdminAuditLog(AdminActions.AffiliateApproved, "affiliate", affiliateId, affiliate.FullName, null, actorIp, _clock.UtcNow));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> BlockAsync(Guid affiliateId, string reason, string? actorIp, CancellationToken cancellationToken)
    {
        var affiliate = await _affiliates.GetByIdAsync(affiliateId, cancellationToken);
        if (affiliate is null) return Result.Failure(Error.NotFound("Afiliant nie istnieje."));

        try { affiliate.Block(reason, _clock.UtcNow); }
        catch (Domain.Common.DomainException ex) { return Result.Failure(Error.Validation(ex.Message)); }

        _admin.AddAdminAudit(new AdminAuditLog(AdminActions.AffiliateBlocked, "affiliate", affiliateId, affiliate.FullName, reason, actorIp, _clock.UtcNow));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ReactivateAsync(Guid affiliateId, string? actorIp, CancellationToken cancellationToken)
    {
        var affiliate = await _affiliates.GetByIdAsync(affiliateId, cancellationToken);
        if (affiliate is null) return Result.Failure(Error.NotFound("Afiliant nie istnieje."));

        try { affiliate.Reactivate(_clock.UtcNow); }
        catch (Domain.Common.DomainException ex) { return Result.Failure(Error.Validation(ex.Message)); }

        _admin.AddAdminAudit(new AdminAuditLog(AdminActions.AffiliateReactivated, "affiliate", affiliateId, affiliate.FullName, null, actorIp, _clock.UtcNow));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> OverrideCommissionAsync(Guid affiliateId, decimal? commissionPercent, int? maxActiveCodes, string? actorIp, CancellationToken cancellationToken)
    {
        var affiliate = await _affiliates.GetByIdAsync(affiliateId, cancellationToken);
        if (affiliate is null) return Result.Failure(Error.NotFound("Afiliant nie istnieje."));

        try
        {
            affiliate.OverrideCommissionPercent(commissionPercent);
            affiliate.OverrideMaxActiveCodes(maxActiveCodes);
        }
        catch (Domain.Common.DomainException ex) { return Result.Failure(Error.Validation(ex.Message)); }

        _admin.AddAdminAudit(new AdminAuditLog(
            AdminActions.AffiliateCommissionOverridden, "affiliate", affiliateId, affiliate.FullName,
            $"commission={commissionPercent?.ToString() ?? "default"} maxCodes={maxActiveCodes?.ToString() ?? "default"}", actorIp, _clock.UtcNow));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>Confirms a Revolut transfer the admin already made by hand (spec §7.1) - deliberately
    /// requires the exact set of AwaitingPayout period ids so a period can never be silently skipped or
    /// double-counted across two payouts.</summary>
    public async Task<Result> MarkPayoutPaidAsync(
        Guid affiliateId, IReadOnlyCollection<Guid> periodIds, decimal amount, string currency,
        string? paymentReference, string? note, string? actorIp, CancellationToken cancellationToken)
    {
        var affiliate = await _affiliates.GetByIdAsync(affiliateId, cancellationToken);
        if (affiliate is null) return Result.Failure(Error.NotFound("Afiliant nie istnieje."));

        var awaitingPeriods = await _periods.ListAwaitingPayoutAsync(affiliateId, cancellationToken);
        var selected = awaitingPeriods.Where(p => periodIds.Contains(p.Id)).ToList();
        if (selected.Count != periodIds.Count)
        {
            return Result.Failure(Error.Validation("Jeden lub więcej wskazanych okresów nie oczekuje już na wypłatę."));
        }

        try
        {
            var payout = new AffiliatePayout(affiliateId, amount, currency, periodIds, paymentReference, note, _clock.UtcNow);
            foreach (var period in selected) period.MarkPaid();
            _payouts.Add(payout);
        }
        catch (Domain.Common.DomainException ex)
        {
            return Result.Failure(Error.Validation(ex.Message));
        }

        _admin.AddAdminAudit(new AdminAuditLog(
            AdminActions.AffiliatePayoutMarkedPaid, "affiliate", affiliateId, affiliate.FullName,
            $"amount={amount} {currency} periods={periodIds.Count} ref={paymentReference}", actorIp, _clock.UtcNow));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>Closes every open period whose calendar month has actually ended (spec §7.1 step 1) -
    /// called by a daily background job, not by any user-facing endpoint.</summary>
    public async Task<int> CloseDuePeriodsAsync(CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var due = await _periods.ListOpenEndingBeforeAsync(now, cancellationToken);
        foreach (var period in due) period.Close(now);
        if (due.Count > 0) await _unitOfWork.SaveChangesAsync(cancellationToken);
        return due.Count;
    }
}
