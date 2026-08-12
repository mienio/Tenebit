using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Assignments;
using Tenebit.Domain.Dashboards;

namespace Tenebit.Application.Dashboard;

public sealed class DashboardService
{
    private readonly IAssetRepository _assets;
    private readonly IPersonRepository _people;
    private readonly IAssignmentRepository _assignments;
    private readonly IAssetCategoryRepository _categories;
    private readonly ITeamRepository _teams;
    private readonly IActivityLogRepository _activity;
    private readonly ILicenseRepository _licenses;
    private readonly IDashboardLayoutRepository _layouts;
    private readonly IDashboardSnapshotRepository _snapshots;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public DashboardService(IAssetRepository assets, IPersonRepository people, IAssignmentRepository assignments, IAssetCategoryRepository categories, ITeamRepository teams, IActivityLogRepository activity, ILicenseRepository licenses, IDashboardLayoutRepository layouts, IDashboardSnapshotRepository snapshots, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork)
    {
        _assets = assets;
        _people = people;
        _assignments = assignments;
        _categories = categories;
        _teams = teams;
        _activity = activity;
        _licenses = licenses;
        _layouts = layouts;
        _snapshots = snapshots;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<DashboardLayoutResponse> GetLayoutAsync(CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(_currentUser.Subject);
        var layout = await _layouts.GetAsync(userId, cancellationToken);
        return new DashboardLayoutResponse(layout?.LayoutJson);
    }

    public async Task<DashboardLayoutResponse> SaveLayoutAsync(SaveDashboardLayoutRequest request, CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(_currentUser.Subject);
        var organizationId = _currentUser.OrganizationId;
        var layout = await _layouts.GetAsync(userId, cancellationToken);
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

    public async Task<DashboardSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var organizationId = _currentUser.OrganizationId;
        var assets = await _assets.ListAsync(organizationId, null, null, null, cancellationToken);
        var people = await _people.ListAsync(organizationId, null, cancellationToken);
        var assignments = await _assignments.ListAsync(organizationId, cancellationToken);
        var categories = await _categories.ListAsync(organizationId, cancellationToken);
        var teams = await _teams.ListAsync(organizationId, cancellationToken);
        var licenses = await _licenses.ListAsync(organizationId, cancellationToken);
        var recent = await _activity.ListAsync(organizationId, 12, cancellationToken);
        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        var limit = today.AddDays(90);

        var byStatus = assets
            .GroupBy(asset => asset.Status)
            .Select(group => new StatusCountResponse(group.Key, group.Count()))
            .OrderBy(item => item.Status.ToString())
            .ToList();

        var warranty = assets
            .Where(asset => asset.WarrantyUntil.HasValue && asset.WarrantyUntil.Value >= today && asset.WarrantyUntil.Value <= limit)
            .OrderBy(asset => asset.WarrantyUntil)
            .Take(8)
            .Select(asset => new UpcomingAssetDateResponse(asset.Id, asset.Name, asset.AssetTag, asset.WarrantyUntil!.Value))
            .ToList();

        var activity = recent
            .Select(log => new RecentActivityResponse(log.Action, log.EntityType, log.EntityId, log.Details, log.ActorSubject, log.CreatedAt))
            .ToList();

        var byCategory = assets
            .GroupBy(asset => asset.CategoryId)
            .Select(group => new CategoryCountResponse(group.Key, categories.FirstOrDefault(c => c.Id == group.Key)?.Name ?? "—", group.Count()))
            .OrderByDescending(item => item.Count)
            .ToList();

        var byLocation = assets
            .Where(asset => !string.IsNullOrWhiteSpace(asset.Location))
            .GroupBy(asset => asset.Location!)
            .Select(group => new LocationCountResponse(group.Key, group.Count()))
            .OrderByDescending(item => item.Count)
            .Take(10)
            .ToList();

        var byTeam = assets
            .Where(asset => asset.TeamId.HasValue)
            .GroupBy(asset => asset.TeamId!.Value)
            .Select(group => new TeamCountResponse(group.Key, teams.FirstOrDefault(t => t.Id == group.Key)?.Name ?? "—", group.Count(), group.Sum(asset => asset.PurchasePrice ?? 0)))
            .OrderByDescending(item => item.Count)
            .ToList();

        return new DashboardSummaryResponse(
            assets.Count,
            assets.Count(asset => asset.Status == AssetStatus.InStock),
            assets.Count(asset => asset.Status == AssetStatus.Assigned),
            assets.Count(asset => asset.Status == AssetStatus.InService),
            assets.Count(asset => asset.AssignedPersonId is null),
            people.Count,
            assignments.Count(assignment => assignment.Status is AssignmentStatus.AwaitingAcceptance or AssignmentStatus.Overdue),
            assignments.Sum(assignment => assignment.ProcedureAcceptances.Count(acceptance => acceptance.Status == AcceptanceStatus.Pending)),
            assets.Sum(asset => asset.PurchasePrice ?? 0),
            licenses.Count,
            licenses.Sum(license => license.Seats.Count),
            licenses.Sum(license => license.SeatsTotal),
            byStatus,
            warranty,
            activity,
            byCategory,
            byLocation,
            byTeam);
    }

    public async Task<Result<DashboardComparisonResponse>> GetComparisonAsync(int daysAgo, CancellationToken cancellationToken)
    {
        var organizationId = _currentUser.OrganizationId;
        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        var targetDate = today.AddDays(-Math.Abs(daysAgo));
        var snapshot = await _snapshots.GetClosestOnOrBeforeAsync(organizationId, targetDate, cancellationToken);
        if (snapshot is null)
        {
            return Result<DashboardComparisonResponse>.Failure(Error.NotFound("Za mało danych historycznych do porównania — migawki zbierają się od teraz, spróbuj ponownie za kilka dni."));
        }

        var current = await GetSummaryAsync(cancellationToken);
        return Result<DashboardComparisonResponse>.Success(new DashboardComparisonResponse(
            snapshot.SnapshotDate,
            current.TotalAssets, snapshot.TotalAssets,
            current.AssetsWithoutOwner, snapshot.AssetsWithoutOwner,
            current.OpenAssignments, snapshot.OpenAssignments,
            current.VisibleAssetValue, snapshot.VisibleAssetValue));
    }
}
