using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Affiliates;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class AffiliateProgramSettingsRepository : IAffiliateProgramSettingsRepository
{
    private readonly TenebitDbContext _db;
    public AffiliateProgramSettingsRepository(TenebitDbContext db) => _db = db;

    public async Task<AffiliateProgramSettings> GetAsync(CancellationToken cancellationToken)
    {
        var existing = await _db.AffiliateProgramSettings.FirstOrDefaultAsync(x => x.Id == AffiliateProgramSettings.SingletonId, cancellationToken);
        if (existing is not null) return existing;

        var created = AffiliateProgramSettings.CreateDefault();
        _db.AffiliateProgramSettings.Add(created);
        await _db.SaveChangesAsync(cancellationToken);
        return created;
    }
}

public sealed class AffiliateCountryDiscountRuleRepository : IAffiliateCountryDiscountRuleRepository
{
    private readonly TenebitDbContext _db;
    public AffiliateCountryDiscountRuleRepository(TenebitDbContext db) => _db = db;

    public async Task<IReadOnlyList<AffiliateCountryDiscountRule>> ListAsync(CancellationToken cancellationToken) =>
        await _db.AffiliateCountryDiscountRules.OrderBy(x => x.CountryCode).ToListAsync(cancellationToken);

    public Task<AffiliateCountryDiscountRule?> GetByCountryAsync(string countryCode, CancellationToken cancellationToken)
    {
        var normalized = countryCode.Trim().ToUpperInvariant();
        return _db.AffiliateCountryDiscountRules.FirstOrDefaultAsync(x => x.CountryCode == normalized, cancellationToken);
    }

    public void Add(AffiliateCountryDiscountRule rule) => _db.AffiliateCountryDiscountRules.Add(rule);
    public void Remove(AffiliateCountryDiscountRule rule) => _db.AffiliateCountryDiscountRules.Remove(rule);
}
