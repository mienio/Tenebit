using Tenebit.Application.Common;
using Tenebit.Domain.Alerts;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Assignments;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Audits;
using Tenebit.Domain.Dashboards;
using Tenebit.Domain.Evidence;
using Tenebit.Domain.Identity;
using Tenebit.Domain.JobProfiles;
using Tenebit.Domain.Licenses;
using Tenebit.Domain.Offboarding;
using Tenebit.Domain.Organizations;
using Tenebit.Domain.People;
using Tenebit.Domain.Procedures;
using Tenebit.Domain.Reservations;
using Tenebit.Domain.Settings;
using Tenebit.Domain.Subscriptions;

namespace Tenebit.Application.Abstractions;

public interface IAssetRepository
{
    Task<IReadOnlyList<Asset>> ListAsync(Guid organizationId, string? search, AssetStatus? status, string? location, CancellationToken cancellationToken);
    Task<IReadOnlyList<Asset>> ListScopedAsync(Guid organizationId, string? search, AssetStatus? status, string? location, IReadOnlyCollection<Guid> personIds, IReadOnlyCollection<Guid> teamIds, CancellationToken cancellationToken);
    Task<(IReadOnlyList<Asset> Items, int Total)> ListPagedAsync(Guid organizationId, string? search, AssetStatus? status, string? location, Guid? teamId, Guid? categoryId, bool unassignedOnly, DateOnly? warrantyFrom, DateOnly? warrantyTo, string? sortKey, bool sortDesc, int page, int pageSize, CancellationToken cancellationToken);
    Task<(IReadOnlyList<Asset> Items, int Total)> ListPagedScopedAsync(Guid organizationId, string? search, AssetStatus? status, string? location, Guid? teamId, Guid? categoryId, bool unassignedOnly, DateOnly? warrantyFrom, DateOnly? warrantyTo, string? sortKey, bool sortDesc, int page, int pageSize, IReadOnlyCollection<Guid> personIds, IReadOnlyCollection<Guid> teamIds, CancellationToken cancellationToken);
    Task<(IReadOnlyDictionary<Guid, int> ByCategory, IReadOnlyDictionary<AssetStatus, int> ByStatus, IReadOnlyDictionary<Guid, int> ByPerson)> GetGroupCountsAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<(IReadOnlyDictionary<Guid, int> ByCategory, IReadOnlyDictionary<AssetStatus, int> ByStatus, IReadOnlyDictionary<Guid, int> ByPerson)> GetGroupCountsScopedAsync(Guid organizationId, IReadOnlyCollection<Guid> personIds, IReadOnlyCollection<Guid> teamIds, CancellationToken cancellationToken);
    Task<IReadOnlyList<Asset>> GetByIdsAsync(Guid organizationId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
    Task<IReadOnlyList<Asset>> ListByAssignedPersonAsync(Guid organizationId, Guid personId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Asset>> ListWarrantyExpiringAsync(Guid organizationId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
    Task<Asset?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    Task<bool> AssetTagExistsAsync(Guid organizationId, string assetTag, Guid? excludingAssetId, CancellationToken cancellationToken);
    Task<int> CountAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<int> CountByLocationAsync(Guid organizationId, string location, CancellationToken cancellationToken);
    Task<int> CountByLocationIdAsync(Guid organizationId, Guid locationId, CancellationToken cancellationToken);
    Task<bool> IsUsedAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    void Add(Asset asset);
    void Remove(Asset asset);
}
