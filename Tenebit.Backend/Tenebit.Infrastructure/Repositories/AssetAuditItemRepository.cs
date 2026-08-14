using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Audits;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class AssetAuditItemRepository : IAssetAuditItemRepository
{
    private readonly TenebitDbContext _db;

    public AssetAuditItemRepository(TenebitDbContext db) => _db = db;

    public async Task<IReadOnlyList<AssetAuditItem>> ListByCampaignAsync(Guid organizationId, Guid campaignId, CancellationToken cancellationToken) =>
        await _db.AssetAuditItems
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.CampaignId == campaignId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AssetAuditItem>> ListByParticipantAsync(Guid organizationId, Guid participantId, CancellationToken cancellationToken) =>
        await _db.AssetAuditItems
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.ParticipantId == participantId)
            .ToListAsync(cancellationToken);

    public void Add(AssetAuditItem item) => _db.AssetAuditItems.Add(item);
}
