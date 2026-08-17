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
            var prefix = normalizedLocation + " / ";
            query = query.Where(x => x.Location == normalizedLocation || (x.Location != null && x.Location.StartsWith(prefix)));
        }

        return await query.OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Asset> Items, int Total)> ListPagedAsync(Guid organizationId, string? search, AssetStatus? status, string? location, Guid? teamId, Guid? categoryId, bool unassignedOnly, DateOnly? warrantyFrom, DateOnly? warrantyTo, string? sortKey, bool sortDesc, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _db.Assets.AsNoTracking().Include(x => x.FieldValues).Where(x => x.OrganizationId == organizationId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x => x.Name.ToLower().Contains(term) || x.AssetTag.ToLower().Contains(term) || (x.SerialNumber != null && x.SerialNumber.ToLower().Contains(term)));
        }

        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(location))
        {
            var normalizedLocation = location.Trim();
            var prefix = normalizedLocation + " / ";
            query = query.Where(x => x.Location == normalizedLocation || (x.Location != null && x.Location.StartsWith(prefix)));
        }

        if (teamId.HasValue) query = query.Where(x => x.TeamId == teamId.Value);
        if (categoryId.HasValue) query = query.Where(x => x.CategoryId == categoryId.Value);
        if (unassignedOnly) query = query.Where(x => x.AssignedPersonId == null);
        if (warrantyFrom.HasValue) query = query.Where(x => x.WarrantyUntil.HasValue && x.WarrantyUntil.Value >= warrantyFrom.Value);
        if (warrantyTo.HasValue) query = query.Where(x => x.WarrantyUntil.HasValue && x.WarrantyUntil.Value <= warrantyTo.Value);

        var total = await query.CountAsync(cancellationToken);

        query = sortKey switch
        {
            "assetTag" => sortDesc ? query.OrderByDescending(x => x.AssetTag) : query.OrderBy(x => x.AssetTag),
            "status" => sortDesc ? query.OrderByDescending(x => x.Status) : query.OrderBy(x => x.Status),
            "location" => sortDesc ? query.OrderByDescending(x => x.Location) : query.OrderBy(x => x.Location),
            "value" => sortDesc ? query.OrderByDescending(x => x.PurchasePrice) : query.OrderBy(x => x.PurchasePrice),
            "warranty" => sortDesc ? query.OrderByDescending(x => x.WarrantyUntil) : query.OrderBy(x => x.WarrantyUntil),
            "person" => sortDesc
                ? query.OrderByDescending(a => _db.People.Where(p => p.Id == a.AssignedPersonId).Select(p => p.LastName + " " + p.FirstName).FirstOrDefault())
                : query.OrderBy(a => _db.People.Where(p => p.Id == a.AssignedPersonId).Select(p => p.LastName + " " + p.FirstName).FirstOrDefault()),
            _ => sortDesc ? query.OrderByDescending(x => x.Name) : query.OrderBy(x => x.Name)
        };

        var items = await query
            .Skip((Math.Max(page, 1) - 1) * Math.Clamp(pageSize, 1, 100))
            .Take(Math.Clamp(pageSize, 1, 100))
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<(IReadOnlyDictionary<Guid, int> ByCategory, IReadOnlyDictionary<AssetStatus, int> ByStatus, IReadOnlyDictionary<Guid, int> ByPerson)> GetGroupCountsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var byCategory = await _db.Assets.AsNoTracking().Where(x => x.OrganizationId == organizationId)
            .GroupBy(x => x.CategoryId)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Id, g => g.Count, cancellationToken);

        var byStatus = await _db.Assets.AsNoTracking().Where(x => x.OrganizationId == organizationId)
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Status, g => g.Count, cancellationToken);

        var byPerson = await _db.Assets.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.AssignedPersonId != null)
            .GroupBy(x => x.AssignedPersonId!.Value)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Id, g => g.Count, cancellationToken);

        return (byCategory, byStatus, byPerson);
    }

    public async Task<IReadOnlyList<Asset>> GetByIdsAsync(Guid organizationId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await _db.Assets.Include(x => x.FieldValues).Where(x => x.OrganizationId == organizationId && ids.Contains(x.Id)).ToListAsync(cancellationToken);

    public Task<Asset?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        _db.Assets.Include(x => x.FieldValues).FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, cancellationToken);

    public Task<bool> AssetTagExistsAsync(Guid organizationId, string assetTag, Guid? excludingAssetId, CancellationToken cancellationToken) =>
        _db.Assets.AnyAsync(x => x.OrganizationId == organizationId && x.AssetTag == assetTag.Trim() && (!excludingAssetId.HasValue || x.Id != excludingAssetId.Value), cancellationToken);

    public Task<int> CountAsync(Guid organizationId, CancellationToken cancellationToken) =>
        _db.Assets.CountAsync(x => x.OrganizationId == organizationId, cancellationToken);

    public Task<int> CountByLocationAsync(Guid organizationId, string location, CancellationToken cancellationToken) =>
        _db.Assets.AsNoTracking().CountAsync(x => x.OrganizationId == organizationId && x.Location == location, cancellationToken);

    public void Add(Asset asset) => _db.Assets.Add(asset);
    public void Remove(Asset asset) => _db.Assets.Remove(asset);
}
