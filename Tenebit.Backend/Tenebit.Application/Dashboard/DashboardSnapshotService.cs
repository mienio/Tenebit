using Tenebit.Application.Abstractions;
using Tenebit.Domain.Assignments;
using Tenebit.Domain.Dashboards;

namespace Tenebit.Application.Dashboard;

public sealed class DashboardSnapshotService
{
    private readonly IOrganizationRepository _organizations;
    private readonly IAssetRepository _assets;
    private readonly IAssignmentRepository _assignments;
    private readonly IDashboardSnapshotRepository _snapshots;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public DashboardSnapshotService(IOrganizationRepository organizations, IAssetRepository assets, IAssignmentRepository assignments, IDashboardSnapshotRepository snapshots, IClock clock, IUnitOfWork unitOfWork)
    {
        _organizations = organizations;
        _assets = assets;
        _assignments = assignments;
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
            // One snapshot per organization per day — safe to run this check multiple times a day.
            if (await _snapshots.GetForDateAsync(organization.Id, today, cancellationToken) is not null) continue;

            var assets = await _assets.ListAsync(organization.Id, null, null, null, cancellationToken);
            var assignments = await _assignments.ListAsync(organization.Id, cancellationToken);

            var snapshot = new DashboardSnapshot(
                organization.Id,
                today,
                totalAssets: assets.Count,
                assetsWithoutOwner: assets.Count(asset => asset.AssignedPersonId is null),
                openAssignments: assignments.Count(assignment => assignment.Status is AssignmentStatus.AwaitingAcceptance or AssignmentStatus.Overdue),
                visibleAssetValue: assets.Sum(asset => asset.PurchasePrice ?? 0),
                createdAt: _clock.UtcNow);

            _snapshots.Add(snapshot);
            hasChanges = true;
        }

        if (hasChanges)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
