using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Audits;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class AssetAuditCampaignRepository : IAssetAuditCampaignRepository
{
    private readonly TenebitDbContext _db;

    public AssetAuditCampaignRepository(TenebitDbContext db) => _db = db;

    public async Task<(IReadOnlyList<AssetAuditCampaign> Items, int Total)> ListPagedAsync(Guid organizationId, AssetAuditCampaignStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _db.AssetAuditCampaigns
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId);

        if (status.HasValue) query = query.Where(x => x.Status == status.Value);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((Math.Max(page, 1) - 1) * Math.Clamp(pageSize, 1, 100))
            .Take(Math.Clamp(pageSize, 1, 100))
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task<AssetAuditCampaign?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        _db.AssetAuditCampaigns.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, cancellationToken);

    public void Add(AssetAuditCampaign campaign) => _db.AssetAuditCampaigns.Add(campaign);
}
