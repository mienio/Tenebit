using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Affiliates;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class AffiliateCodeRepository : IAffiliateCodeRepository
{
    private readonly TenebitDbContext _db;
    public AffiliateCodeRepository(TenebitDbContext db) => _db = db;

    public Task<AffiliateCode?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return _db.AffiliateCodes.FirstOrDefaultAsync(x => x.Code == normalized, cancellationToken);
    }

    public Task<AffiliateCode?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _db.AffiliateCodes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<AffiliateCode>> ListByAffiliateAsync(Guid affiliateId, CancellationToken cancellationToken) =>
        await _db.AffiliateCodes.Where(x => x.AffiliateId == affiliateId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<int> CountActiveByAffiliateAsync(Guid affiliateId, CancellationToken cancellationToken) =>
        _db.AffiliateCodes.CountAsync(x => x.AffiliateId == affiliateId && x.IsActive, cancellationToken);

    public void Add(AffiliateCode code) => _db.AffiliateCodes.Add(code);
}
