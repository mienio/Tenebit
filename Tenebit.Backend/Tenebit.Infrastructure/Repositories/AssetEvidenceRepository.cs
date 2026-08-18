using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Evidence;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class AssetEvidenceRepository : IAssetEvidenceRepository
{
    private readonly TenebitDbContext _db;
    public AssetEvidenceRepository(TenebitDbContext db) => _db = db;

    public async Task<IReadOnlyList<AssetEvidence>> ListByAssetAsync(Guid organizationId, Guid assetId, CancellationToken cancellationToken) =>
        await _db.AssetEvidence.Where(x => x.OrganizationId == organizationId && x.AssetId == assetId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AssetEvidence>> ListByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken) =>
        await _db.AssetEvidence.Where(x => x.OrganizationId == organizationId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AssetEvidence>> ListByAssignmentIdsAsync(Guid organizationId, IReadOnlyCollection<Guid> assignmentIds, CancellationToken cancellationToken) =>
        await _db.AssetEvidence.Where(x => x.OrganizationId == organizationId && x.AssignmentId.HasValue && assignmentIds.Contains(x.AssignmentId.Value)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AssetEvidence>> ListByAssignmentAsync(Guid organizationId, Guid assignmentId, CancellationToken cancellationToken) =>
        await _db.AssetEvidence.Where(x => x.OrganizationId == organizationId && x.AssignmentId == assignmentId).ToListAsync(cancellationToken);

    public Task<AssetEvidence?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        _db.AssetEvidence.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, cancellationToken);

    public Task<int> CountAsync(Guid organizationId, Guid assetId, EvidencePhase phase, CancellationToken cancellationToken) =>
        _db.AssetEvidence.CountAsync(x => x.OrganizationId == organizationId && x.AssetId == assetId && x.Phase == phase, cancellationToken);

    public void Add(AssetEvidence evidence) => _db.AssetEvidence.Add(evidence);

    public void Remove(AssetEvidence evidence) => _db.AssetEvidence.Remove(evidence);
}
