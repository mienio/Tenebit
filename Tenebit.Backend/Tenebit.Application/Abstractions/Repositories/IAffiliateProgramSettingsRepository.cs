using Tenebit.Domain.Affiliates;

namespace Tenebit.Application.Abstractions;

public interface IAffiliateProgramSettingsRepository
{
    /// <summary>Returns the single settings row, creating and persisting the default one on first
    /// access if it does not exist yet (so a fresh environment never needs a manual seed step).</summary>
    Task<AffiliateProgramSettings> GetAsync(CancellationToken cancellationToken);
}

public interface IAffiliateCountryDiscountRuleRepository
{
    Task<IReadOnlyList<AffiliateCountryDiscountRule>> ListAsync(CancellationToken cancellationToken);
    Task<AffiliateCountryDiscountRule?> GetByCountryAsync(string countryCode, CancellationToken cancellationToken);
    void Add(AffiliateCountryDiscountRule rule);
    void Remove(AffiliateCountryDiscountRule rule);
}
