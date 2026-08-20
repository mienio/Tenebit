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
        var query = ApplyBasicFilters(BaseQuery(organizationId), search, status, location);
        return await query.OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Asset>> ListScopedAsync(Guid organizationId, string? search, AssetStatus? status, string? location, IReadOnlyCollection<Guid> personIds, IReadOnlyCollection<Guid> teamIds, CancellationToken cancellationToken)
    {
        var query = ApplyManagerScope(BaseQuery(organizationId), personIds, teamIds);
        query = ApplyBasicFilters(query, search, status, location);
        return await query.OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public Task<(IReadOnlyList<Asset> Items, int Total)> ListPagedAsync(Guid organizationId, string? search, AssetStatus? status, string? location, Guid? teamId, Guid? categoryId, bool unassignedOnly, DateOnly? warrantyFrom, DateOnly? warrantyTo, string? sortKey, bool sortDesc, int page, int pageSize, CancellationToken cancellationToken) =>
        ListPagedCoreAsync(BaseQuery(organizationId), organizationId, search, status, location, teamId, categoryId, unassignedOnly, warrantyFrom, warrantyTo, sortKey, sortDesc, page, pageSize, cancellationToken);

    public Task<(IReadOnlyList<Asset> Items, int Total)> ListPagedScopedAsync(Guid organizationId, string? search, AssetStatus? status, string? location, Guid? teamId, Guid? categoryId, bool unassignedOnly, DateOnly? warrantyFrom, DateOnly? warrantyTo, string? sortKey, bool sortDesc, int page, int pageSize, IReadOnlyCollection<Guid> personIds, IReadOnlyCollection<Guid> teamIds, CancellationToken cancellationToken) =>
        ListPagedCoreAsync(ApplyManagerScope(BaseQuery(organizationId), personIds, teamIds), organizationId, search, status, location, teamId, categoryId, unassignedOnly, warrantyFrom, warrantyTo, sortKey, sortDesc, page, pageSize, cancellationToken);

    public Task<(IReadOnlyDictionary<Guid, int> ByCategory, IReadOnlyDictionary<AssetStatus, int> ByStatus, IReadOnlyDictionary<Guid, int> ByPerson)> GetGroupCountsAsync(Guid organizationId, CancellationToken cancellationToken) =>
        GetGroupCountsCoreAsync(_db.Assets.AsNoTracking().Where(x => x.OrganizationId == organizationId), cancellationToken);

    public Task<(IReadOnlyDictionary<Guid, int> ByCategory, IReadOnlyDictionary<AssetStatus, int> ByStatus, IReadOnlyDictionary<Guid, int> ByPerson)> GetGroupCountsScopedAsync(Guid organizationId, IReadOnlyCollection<Guid> personIds, IReadOnlyCollection<Guid> teamIds, CancellationToken cancellationToken) =>
        GetGroupCountsCoreAsync(ApplyManagerScope(_db.Assets.AsNoTracking().Where(x => x.OrganizationId == organizationId), personIds, teamIds), cancellationToken);

    public async Task<IReadOnlyList<Asset>> GetByIdsAsync(Guid organizationId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await _db.Assets.Include(x => x.FieldValues).Where(x => x.OrganizationId == organizationId && ids.Contains(x.Id)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Asset>> ListByAssignedPersonAsync(Guid organizationId, Guid personId, CancellationToken cancellationToken) =>
        await _db.Assets.AsNoTracking().Include(x => x.FieldValues)
            .Where(x => x.OrganizationId == organizationId && x.AssignedPersonId == personId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Asset>> ListWarrantyExpiringAsync(Guid organizationId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        await _db.Assets.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.WarrantyUntil.HasValue && x.WarrantyUntil.Value >= from && x.WarrantyUntil.Value <= to)
            .OrderBy(x => x.WarrantyUntil)
            .ToListAsync(cancellationToken);

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

    private IQueryable<Asset> BaseQuery(Guid organizationId) =>
        _db.Assets.AsNoTracking().Include(x => x.FieldValues).Where(x => x.OrganizationId == organizationId);

    private static IQueryable<Asset> ApplyManagerScope(IQueryable<Asset> query, IReadOnlyCollection<Guid> personIds, IReadOnlyCollection<Guid> teamIds) =>
        query.Where(asset =>
            (asset.AssignedPersonId.HasValue && personIds.Contains(asset.AssignedPersonId.Value))
            || (asset.TeamId.HasValue && teamIds.Contains(asset.TeamId.Value)));

    private static IQueryable<Asset> ApplyBasicFilters(IQueryable<Asset> query, string? search, AssetStatus? status, string? location)
    {
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

        return query;
    }

    private async Task<(IReadOnlyList<Asset> Items, int Total)> ListPagedCoreAsync(
        IQueryable<Asset> query,
        Guid organizationId,
        string? search,
        AssetStatus? status,
        string? location,
        Guid? teamId,
        Guid? categoryId,
        bool unassignedOnly,
        DateOnly? warrantyFrom,
        DateOnly? warrantyTo,
        string? sortKey,
        bool sortDesc,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        query = ApplyBasicFilters(query, search, status, location);
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
                ? query.OrderByDescending(a => _db.People.Where(p => p.OrganizationId == organizationId && p.Id == a.AssignedPersonId).Select(p => p.LastName + " " + p.FirstName).FirstOrDefault())
                : query.OrderBy(a => _db.People.Where(p => p.OrganizationId == organizationId && p.Id == a.AssignedPersonId).Select(p => p.LastName + " " + p.FirstName).FirstOrDefault()),
            _ => sortDesc ? query.OrderByDescending(x => x.Name) : query.OrderBy(x => x.Name)
        };

        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var items = await query.Skip((safePage - 1) * safePageSize).Take(safePageSize).ToListAsync(cancellationToken);
        return (items, total);
    }

    private static async Task<(IReadOnlyDictionary<Guid, int> ByCategory, IReadOnlyDictionary<AssetStatus, int> ByStatus, IReadOnlyDictionary<Guid, int> ByPerson)> GetGroupCountsCoreAsync(IQueryable<Asset> query, CancellationToken cancellationToken)
    {
        var byCategory = await query.GroupBy(x => x.CategoryId)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Id, g => g.Count, cancellationToken);

        var byStatus = await query.GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Status, g => g.Count, cancellationToken);

        var byPerson = await query.Where(x => x.AssignedPersonId != null)
            .GroupBy(x => x.AssignedPersonId!.Value)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Id, g => g.Count, cancellationToken);

        return (byCategory, byStatus, byPerson);
    }

    public Task<int> CountByLocationIdAsync(Guid organizationId, Guid locationId, CancellationToken cancellationToken) =>
        _db.Assets.CountAsync(x => x.OrganizationId == organizationId && x.LocationId == locationId, cancellationToken);

    public async Task<bool> IsUsedAsync(Guid organizationId, Guid id, CancellationToken cancellationToken)
    {
        if (await _db.Assignments.AnyAsync(a => a.OrganizationId == organizationId && a.Assets.Any(x => x.AssetId == id), cancellationToken)) return true;
        if (await _db.AssetAuditItems.AnyAsync(x => x.OrganizationId == organizationId && x.AssetId == id, cancellationToken)) return true;
        if (await _db.EquipmentReservationItems.AnyAsync(x => x.OrganizationId == organizationId && (x.AssetId == id || x.OriginalAssetId == id), cancellationToken)) return true;
        if (await _db.OffboardingItems.AnyAsync(x => x.OrganizationId == organizationId && x.AssetId == id, cancellationToken)) return true;
        if (await _db.AssetInspections.AnyAsync(x => x.OrganizationId == organizationId && x.AssetId == id, cancellationToken)) return true;
        if (await _db.ServiceTickets.AnyAsync(x => x.OrganizationId == organizationId && x.AssetId == id, cancellationToken)) return true;
        return false;
    }
}
