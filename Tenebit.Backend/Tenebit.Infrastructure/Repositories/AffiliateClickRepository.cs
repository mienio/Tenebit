using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Affiliates;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class AffiliateClickRepository : IAffiliateClickRepository
{
    private readonly TenebitDbContext _db;
    public AffiliateClickRepository(TenebitDbContext db) => _db = db;

    public Task<int> CountRecentAsync(Guid affiliateCodeId, string ipHash, DateTimeOffset since, CancellationToken cancellationToken) =>
        _db.AffiliateClicks.CountAsync(x => x.AffiliateCodeId == affiliateCodeId && x.IpHash == ipHash && x.ClickedAt >= since, cancellationToken);

    public void Add(AffiliateClick click) => _db.AffiliateClicks.Add(click);
}
