using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Affiliates;
using Tenebit.Domain.Identity;

namespace Tenebit.Application.Admin;

public sealed record AffiliateProgramSettingsResponse(
    decimal DefaultCommissionPercent, string CommissionBase, int? DefaultCommissionWindowMonths,
    int DefaultMaxCodesPerAffiliate, int PayoutDayOfMonth, int PayoutGraceDays, decimal? MinimumPayoutAmount,
    bool CodeGrantsCustomerDiscountByDefault, decimal? DefaultCustomerDiscountPercent, int? DefaultCustomerDiscountDurationMonths,
    bool PublicLeaderboardEnabled, string TermsVersion);

public sealed record AffiliateCountryDiscountRuleResponse(Guid Id, string CountryCode, decimal DiscountPercent, int? DurationMonths);

/// <summary>Global program configuration (spec §9.3) - every field here is a business decision the
/// product owner can tune without a deployment (commission rate, payout day, whether a code discounts
/// the customer). See AffiliateProgramSettings.CreateDefault for the values a fresh environment starts
/// with.</summary>
public sealed class AffiliateProgramSettingsAdminService
{
    private readonly IAffiliateProgramSettingsRepository _settings;
    private readonly IAffiliateCountryDiscountRuleRepository _countryRules;
    private readonly IAdminRepository _admin;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public AffiliateProgramSettingsAdminService(
        IAffiliateProgramSettingsRepository settings, IAffiliateCountryDiscountRuleRepository countryRules,
        IAdminRepository admin, IUnitOfWork unitOfWork, IClock clock)
    {
        _settings = settings;
        _countryRules = countryRules;
        _admin = admin;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<AffiliateProgramSettingsResponse> GetAsync(CancellationToken cancellationToken) => ToResponse(await _settings.GetAsync(cancellationToken));

    public async Task<Result<AffiliateProgramSettingsResponse>> UpdateAsync(
        decimal defaultCommissionPercent, AffiliateCommissionBase commissionBase, int? defaultCommissionWindowMonths,
        int defaultMaxCodesPerAffiliate, int payoutDayOfMonth, int payoutGraceDays, decimal? minimumPayoutAmount,
        bool codeGrantsCustomerDiscountByDefault, decimal? defaultCustomerDiscountPercent, int? defaultCustomerDiscountDurationMonths,
        bool publicLeaderboardEnabled, string? actorIp, CancellationToken cancellationToken)
    {
        var settings = await _settings.GetAsync(cancellationToken);
        try
        {
            settings.Update(
                defaultCommissionPercent, commissionBase, defaultCommissionWindowMonths, defaultMaxCodesPerAffiliate,
                payoutDayOfMonth, payoutGraceDays, minimumPayoutAmount, codeGrantsCustomerDiscountByDefault,
                defaultCustomerDiscountPercent, defaultCustomerDiscountDurationMonths, publicLeaderboardEnabled);
        }
        catch (Domain.Common.DomainException ex)
        {
            return Result<AffiliateProgramSettingsResponse>.Failure(Error.Validation(ex.Message));
        }

        _admin.AddAdminAudit(new AdminAuditLog(AdminActions.AffiliateSettingsUpdated, "affiliate_settings", null, null, null, actorIp, _clock.UtcNow));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<AffiliateProgramSettingsResponse>.Success(ToResponse(settings));
    }

    public async Task<IReadOnlyList<AffiliateCountryDiscountRuleResponse>> ListCountryRulesAsync(CancellationToken cancellationToken) =>
        (await _countryRules.ListAsync(cancellationToken)).Select(ToRuleResponse).ToList();

    public async Task<Result<AffiliateCountryDiscountRuleResponse>> UpsertCountryRuleAsync(string countryCode, decimal discountPercent, int? durationMonths, CancellationToken cancellationToken)
    {
        try
        {
            var existing = await _countryRules.GetByCountryAsync(countryCode, cancellationToken);
            if (existing is not null)
            {
                existing.Update(discountPercent, durationMonths);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return Result<AffiliateCountryDiscountRuleResponse>.Success(ToRuleResponse(existing));
            }

            var rule = new AffiliateCountryDiscountRule(countryCode, discountPercent, durationMonths, _clock.UtcNow);
            _countryRules.Add(rule);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<AffiliateCountryDiscountRuleResponse>.Success(ToRuleResponse(rule));
        }
        catch (Domain.Common.DomainException ex)
        {
            return Result<AffiliateCountryDiscountRuleResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    public async Task<Result> RemoveCountryRuleAsync(Guid ruleId, CancellationToken cancellationToken)
    {
        var rules = await _countryRules.ListAsync(cancellationToken);
        var rule = rules.FirstOrDefault(r => r.Id == ruleId);
        if (rule is null) return Result.Failure(Error.NotFound("Reguła nie istnieje."));
        _countryRules.Remove(rule);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static AffiliateProgramSettingsResponse ToResponse(AffiliateProgramSettings s) => new(
        s.DefaultCommissionPercent, s.CommissionBase.ToString(), s.DefaultCommissionWindowMonths, s.DefaultMaxCodesPerAffiliate,
        s.PayoutDayOfMonth, s.PayoutGraceDays, s.MinimumPayoutAmount, s.CodeGrantsCustomerDiscountByDefault,
        s.DefaultCustomerDiscountPercent, s.DefaultCustomerDiscountDurationMonths,
        s.PublicLeaderboardEnabled, s.TermsVersion);

    private static AffiliateCountryDiscountRuleResponse ToRuleResponse(AffiliateCountryDiscountRule r) => new(r.Id, r.CountryCode, r.DiscountPercent, r.DurationMonths);
}
