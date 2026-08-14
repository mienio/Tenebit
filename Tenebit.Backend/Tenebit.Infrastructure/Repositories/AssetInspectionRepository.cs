using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Assets;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class AssetInspectionRepository : IAssetInspectionRepository
{
    private readonly TenebitDbContext _db;
    public AssetInspectionRepository(TenebitDbContext db) => _db = db;

    public Task<AssetInspection?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        _db.AssetInspections.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, cancellationToken);

    public Task<AssetInspection?> GetPendingByAssetAsync(Guid organizationId, Guid assetId, CancellationToken cancellationToken) =>
        _db.AssetInspections
            .Where(x => x.OrganizationId == organizationId && x.AssetId == assetId && x.Outcome == null)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public void Add(AssetInspection inspection) => _db.AssetInspections.Add(inspection);
}
