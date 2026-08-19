using Tenebit.Application.Abstractions;
using Tenebit.Domain.Dashboards;

namespace Tenebit.Application.Dashboard;

public sealed class DashboardSnapshotService
{
    private readonly IOrganizationRepository _organizations;
    private readonly IDashboardReadRepository _dashboardRead;
    private readonly IDashboardSnapshotRepository _snapshots;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public DashboardSnapshotService(IOrganizationRepository organizations, IDashboardReadRepository dashboardRead, IDashboardSnapshotRepository snapshots, IClock clock, IUnitOfWork unitOfWork)
    {
        _organizations = organizations;
        _dashboardRead = dashboardRead;
        _snapshots = snapshots;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task CaptureAllOrganizationsAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        var organizations = await _organizations.ListAllAsync(cancellationToken);
        var hasChanges = false;

        foreach (var organization in organizations)
        {
            if (await _snapshots.GetForDateAsync(organization.Id, today, cancellationToken) is not null) continue;

            var metrics = await _dashboardRead.GetSnapshotMetricsAsync(organization.Id, cancellationToken);
            var snapshot = new DashboardSnapshot(
                organization.Id,
                today,
                metrics.TotalAssets,
                metrics.AssetsWithoutOwner,
                metrics.OpenAssignments,
                metrics.VisibleAssetValue,
                _clock.UtcNow);

            _snapshots.Add(snapshot);
            hasChanges = true;
        }

        if (hasChanges)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
