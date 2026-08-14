using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Audits;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class AssetAuditParticipantRepository : IAssetAuditParticipantRepository
{
    private readonly TenebitDbContext _db;

    public AssetAuditParticipantRepository(TenebitDbContext db) => _db = db;

    public async Task<IReadOnlyList<AssetAuditParticipant>> ListByCampaignAsync(Guid organizationId, Guid campaignId, CancellationToken cancellationToken) =>
        await _db.AssetAuditParticipants
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.CampaignId == campaignId)
            .ToListAsync(cancellationToken);

    public async Task<AssetAuditParticipant?> GetAsync(Guid organizationId, Guid campaignId, Guid participantId, CancellationToken cancellationToken) =>
        await _db.AssetAuditParticipants
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.CampaignId == campaignId && x.Id == participantId, cancellationToken);

    public void Add(AssetAuditParticipant participant) => _db.AssetAuditParticipants.Add(participant);

    public async Task<IReadOnlyList<AssetAuditParticipant>> ListWithActiveTokenAsync(CancellationToken cancellationToken) =>
        await _db.AssetAuditParticipants
            .Where(x => x.TokenHash != null && x.TokenRevokedAt == null)
            .ToListAsync(cancellationToken);
}
