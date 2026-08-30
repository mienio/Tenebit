using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Assignments;
using Tenebit.Domain.Dashboards;

namespace Tenebit.Application.Dashboard;

public sealed class DashboardService
{
    private readonly IDashboardReadRepository _dashboardRead;
    private readonly IActivityLogRepository _activity;
    private readonly IDashboardLayoutRepository _layouts;
    private readonly IDashboardSnapshotRepository _snapshots;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public DashboardService(IDashboardReadRepository dashboardRead, IActivityLogRepository activity, IDashboardLayoutRepository layouts, IDashboardSnapshotRepository snapshots, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork)
    {
        _dashboardRead = dashboardRead;
        _activity = activity;
        _layouts = layouts;
        _snapshots = snapshots;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<DashboardLayoutResponse> GetLayoutAsync(CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(_currentUser.Subject);
        var layout = await _layouts.GetAsync(_currentUser.OrganizationId, userId, cancellationToken);
        return new DashboardLayoutResponse(layout?.LayoutJson);
    }

    public async Task<DashboardLayoutResponse> SaveLayoutAsync(SaveDashboardLayoutRequest request, CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(_currentUser.Subject);
        var organizationId = _currentUser.OrganizationId;
        var layout = await _layouts.GetAsync(organizationId, userId, cancellationToken);
        if (layout is null)
        {
            layout = new DashboardLayout(organizationId, userId, request.LayoutJson);
            _layouts.Add(layout);
        }
        else
        {
            layout.Update(request.LayoutJson);
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new DashboardLayoutResponse(layout.LayoutJson);
    }

    public async Task<Result<DashboardSummaryResponse>> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.DashboardViewers);
        if (access.IsFailure) return Result<DashboardSummaryResponse>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        var summary = await _dashboardRead.GetSummaryAsync(organizationId, today, today.AddDays(90), cancellationToken);
        var recent = await _activity.ListAsync(organizationId, 12, cancellationToken);

        var activity = recent
            .Select(log => new RecentActivityResponse(log.Action, log.EntityType, log.EntityId, log.Details, log.ActorSubject, log.CreatedAt))
            .ToList();

        return Result<DashboardSummaryResponse>.Success(new DashboardSummaryResponse(
            summary.TotalAssets,
            summary.AssetsInStock,
            summary.AssetsAssigned,
            summary.AssetsInService,
            summary.AssetsWithoutOwner,
            summary.PeopleCount,
            summary.OpenAssignments,
            summary.PendingProcedureAcceptances,
            summary.VisibleAssetValue,
            summary.TotalLicenses,
            summary.LicenseSeatsUsed,
            summary.LicenseSeatsTotal,
            summary.AssetsByStatus.Select(x => new StatusCountResponse(x.Status, x.Count)).ToList(),
            summary.WarrantyExpiringSoon.Select(x => new UpcomingAssetDateResponse(x.AssetId, x.Name, x.AssetTag, x.WarrantyUntil)).ToList(),
            activity,
            summary.AssetsByCategory.Select(x => new CategoryCountResponse(x.CategoryId, x.CategoryName, x.Count)).ToList(),
            summary.AssetsByLocation.Select(x => new LocationCountResponse(x.Location, x.Count)).ToList(),
            summary.AssetsByTeam.Select(x => new TeamCountResponse(x.TeamId, x.TeamName, x.Count, x.TotalValue)).ToList()));
    }

    public async Task<Result<DashboardComparisonResponse>> GetComparisonAsync(int daysAgo, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.DashboardViewers);
        if (access.IsFailure) return Result<DashboardComparisonResponse>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        var targetDate = today.AddDays(-Math.Abs(daysAgo));
        var snapshot = await _snapshots.GetClosestOnOrBeforeAsync(organizationId, targetDate, cancellationToken);
        if (snapshot is null)
        {
            return Result<DashboardComparisonResponse>.Failure(Error.NotFound("Za mało danych historycznych do porównania - migawki zbierają się od teraz, spróbuj ponownie za kilka dni."));
        }

        var current = await GetSummaryAsync(cancellationToken);
        if (current.IsFailure) return Result<DashboardComparisonResponse>.Failure(current.Error!);

        return Result<DashboardComparisonResponse>.Success(new DashboardComparisonResponse(
            snapshot.SnapshotDate,
            current.Value!.TotalAssets, snapshot.TotalAssets,
            current.Value!.AssetsWithoutOwner, snapshot.AssetsWithoutOwner,
            current.Value!.OpenAssignments, snapshot.OpenAssignments,
            current.Value!.VisibleAssetValue, snapshot.VisibleAssetValue));
    }
}
