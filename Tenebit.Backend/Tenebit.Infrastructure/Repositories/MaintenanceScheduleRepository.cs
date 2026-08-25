using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Assets;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class MaintenanceScheduleRepository : IMaintenanceScheduleRepository
{
    private readonly TenebitDbContext _db;

    public MaintenanceScheduleRepository(TenebitDbContext db) => _db = db;

    public async Task<IReadOnlyList<MaintenanceSchedule>> ListAsync(Guid organizationId, CancellationToken cancellationToken) =>
        await _db.MaintenanceSchedules
            .Where(x => x.OrganizationId == organizationId)
            .OrderBy(x => x.NextDueOn)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MaintenanceSchedule>> ListByAssetAsync(Guid organizationId, Guid assetId, CancellationToken cancellationToken) =>
        await _db.MaintenanceSchedules
            .Where(x => x.OrganizationId == organizationId && x.AssetId == assetId)
            .OrderBy(x => x.NextDueOn)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MaintenanceSchedule>> ListDueAsync(Guid organizationId, DateOnly through, CancellationToken cancellationToken) =>
        await _db.MaintenanceSchedules
            .Where(x => x.OrganizationId == organizationId && x.IsActive && x.NextDueOn <= through)
            .OrderBy(x => x.NextDueOn)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, DateOnly>> GetEarliestDueByAssetAsync(Guid organizationId, IReadOnlyCollection<Guid> assetIds, CancellationToken cancellationToken)
    {
        if (assetIds.Count == 0) return new Dictionary<Guid, DateOnly>();

        var rows = await _db.MaintenanceSchedules
            .Where(x => x.OrganizationId == organizationId && x.IsActive && assetIds.Contains(x.AssetId))
            .GroupBy(x => x.AssetId)
            .Select(g => new { AssetId = g.Key, Earliest = g.Min(x => x.NextDueOn) })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(x => x.AssetId, x => x.Earliest);
    }

    public Task<MaintenanceSchedule?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        _db.MaintenanceSchedules.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, cancellationToken);

    public void Add(MaintenanceSchedule schedule) => _db.MaintenanceSchedules.Add(schedule);

    public void Remove(MaintenanceSchedule schedule) => _db.MaintenanceSchedules.Remove(schedule);
}
