using Tenebit.Application.Abstractions;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Assignments;

namespace Tenebit.Application.Dashboard;

public sealed class DashboardService
{
    private readonly IAssetRepository _assets;
    private readonly IPersonRepository _people;
    private readonly IAssignmentRepository _assignments;
    private readonly IAssetCategoryRepository _categories;
    private readonly ITeamRepository _teams;
    private readonly IActivityLogRepository _activity;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public DashboardService(IAssetRepository assets, IPersonRepository people, IAssignmentRepository assignments, IAssetCategoryRepository categories, ITeamRepository teams, IActivityLogRepository activity, ICurrentUser currentUser, IClock clock)
    {
        _assets = assets;
        _people = people;
        _assignments = assignments;
        _categories = categories;
        _teams = teams;
        _activity = activity;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<DashboardSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var organizationId = _currentUser.OrganizationId;
        var assets = await _assets.ListAsync(organizationId, null, null, null, cancellationToken);
        var people = await _people.ListAsync(organizationId, null, cancellationToken);
        var assignments = await _assignments.ListAsync(organizationId, cancellationToken);
        var categories = await _categories.ListAsync(organizationId, cancellationToken);
        var teams = await _teams.ListAsync(organizationId, cancellationToken);
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
            byStatus,
            warranty,
            activity,
            byCategory,
            byLocation,
            byTeam);
    }
}
