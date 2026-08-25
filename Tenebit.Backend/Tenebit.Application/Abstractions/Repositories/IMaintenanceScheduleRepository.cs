using Tenebit.Domain.Assets;

namespace Tenebit.Application.Abstractions;

public interface IMaintenanceScheduleRepository
{
    Task<IReadOnlyList<MaintenanceSchedule>> ListAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<MaintenanceSchedule>> ListByAssetAsync(Guid organizationId, Guid assetId, CancellationToken cancellationToken);

    /// <summary>Active schedules due on or before <paramref name="through"/>, soonest first - powers both
    /// the dashboard panel and the alert sweep.</summary>
    Task<IReadOnlyList<MaintenanceSchedule>> ListDueAsync(Guid organizationId, DateOnly through, CancellationToken cancellationToken);

    Task<MaintenanceSchedule?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    void Add(MaintenanceSchedule schedule);
    void Remove(MaintenanceSchedule schedule);
}
