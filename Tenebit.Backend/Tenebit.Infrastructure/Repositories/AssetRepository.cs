using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Assets;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class AssetRepository : IAssetRepository
{
    private readonly TenebitDbContext _db;

    public AssetRepository(TenebitDbContext db) => _db = db;

    public async Task<IReadOnlyList<Asset>> ListAsync(Guid organizationId, string? search, AssetStatus? status, string? location, CancellationToken cancellationToken)
    {
        var query = _db.Assets.AsNoTracking().Include(x => x.FieldValues).Where(x => x.OrganizationId == organizationId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x => x.Name.ToLower().Contains(term) || x.AssetTag.ToLower().Contains(term) || (x.SerialNumber != null && x.SerialNumber.ToLower().Contains(term)));
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            var normalizedLocation = location.Trim();
            query = query.Where(x => x.Location == normalizedLocation);
        }

        return await query.OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Asset>> GetByIdsAsync(Guid organizationId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await _db.Assets.Include(x => x.FieldValues).Where(x => x.OrganizationId == organizationId && ids.Contains(x.Id)).ToListAsync(cancellationToken);

    public Task<Asset?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        _db.Assets.Include(x => x.FieldValues).FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, cancellationToken);

    public Task<bool> AssetTagExistsAsync(Guid organizationId, string assetTag, Guid? excludingAssetId, CancellationToken cancellationToken) =>
        _db.Assets.AnyAsync(x => x.OrganizationId == organizationId && x.AssetTag == assetTag.Trim() && (!excludingAssetId.HasValue || x.Id != excludingAssetId.Value), cancellationToken);

    public void Add(Asset asset) => _db.Assets.Add(asset);
    public void Remove(Asset asset) => _db.Assets.Remove(asset);
}
