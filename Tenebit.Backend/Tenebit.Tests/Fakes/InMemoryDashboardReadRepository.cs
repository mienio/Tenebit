using Tenebit.Application.Abstractions;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Assignments;

namespace Tenebit.Tests.Fakes;

public sealed class InMemoryDashboardReadRepository : IDashboardReadRepository
{
    private readonly InMemoryAssetRepository _assets;
    private readonly InMemoryPersonRepository _people;
    private readonly InMemoryAssignmentRepository _assignments;
    private readonly InMemoryAssetCategoryRepository _categories;
    private readonly InMemoryTeamRepository _teams;
    private readonly InMemoryLicenseRepository _licenses;

    public InMemoryDashboardReadRepository(
        InMemoryAssetRepository assets,
        InMemoryPersonRepository? people = null,
        InMemoryAssignmentRepository? assignments = null,
        InMemoryAssetCategoryRepository? categories = null,
        InMemoryTeamRepository? teams = null,
        InMemoryLicenseRepository? licenses = null)
    {
        _assets = assets;
        _people = people ?? new InMemoryPersonRepository();
        _assignments = assignments ?? new InMemoryAssignmentRepository();
        _categories = categories ?? new InMemoryAssetCategoryRepository();
        _teams = teams ?? new InMemoryTeamRepository();
        _licenses = licenses ?? new InMemoryLicenseRepository();
    }

    public Task<DashboardReadModel> GetSummaryAsync(Guid organizationId, DateOnly today, DateOnly warrantyLimit, CancellationToken cancellationToken)
    {
        var assets = _assets.Assets.Where(x => x.OrganizationId == organizationId).ToList();
        var assignments = _assignments.Assignments.Where(x => x.OrganizationId == organizationId).ToList();
        var licenses = _licenses.Licenses.Where(x => x.OrganizationId == organizationId).ToList();

        var model = new DashboardReadModel(
            assets.Count,
            assets.Count(x => x.Status == AssetStatus.InStock),
            assets.Count(x => x.Status == AssetStatus.Assigned),
            assets.Count(x => x.Status == AssetStatus.InService),
            assets.Count(x => x.AssignedPersonId is null),
            _people.People.Count(x => x.OrganizationId == organizationId),
            assignments.Count(x => x.Status is AssignmentStatus.AwaitingAcceptance or AssignmentStatus.Overdue),
            assignments.Sum(x => x.ProcedureAcceptances.Count(a => a.Status == AcceptanceStatus.Pending)),
            assets.Sum(x => x.PurchasePrice ?? 0m),
            licenses.Count,
            licenses.Sum(x => x.Seats.Count),
            licenses.Sum(x => x.SeatsTotal),
            assets.GroupBy(x => x.Status).Select(x => new DashboardStatusCount(x.Key, x.Count())).ToList(),
            assets.Where(x => x.WarrantyUntil.HasValue && x.WarrantyUntil.Value >= today && x.WarrantyUntil.Value <= warrantyLimit)
                .OrderBy(x => x.WarrantyUntil).Take(8)
                .Select(x => new DashboardWarrantyAsset(x.Id, x.Name, x.AssetTag, x.WarrantyUntil!.Value)).ToList(),
            assets.GroupBy(x => x.CategoryId)
                .Select(x => new DashboardCategoryCount(x.Key, _categories.Categories.FirstOrDefault(c => c.Id == x.Key)?.Name ?? "-", x.Count()))
                .OrderByDescending(x => x.Count).ToList(),
            assets.Where(x => !string.IsNullOrWhiteSpace(x.Location)).GroupBy(x => x.Location!)
                .Select(x => new DashboardLocationCount(x.Key, x.Count())).OrderByDescending(x => x.Count).Take(10).ToList(),
            assets.Where(x => x.TeamId.HasValue).GroupBy(x => x.TeamId!.Value)
                .Select(x => new DashboardTeamCount(x.Key, _teams.Teams.FirstOrDefault(t => t.Id == x.Key)?.Name ?? "-", x.Count(), x.Sum(a => a.PurchasePrice ?? 0m)))
                .OrderByDescending(x => x.Count).ToList());

        return Task.FromResult(model);
    }

    public Task<DashboardSnapshotMetrics> GetSnapshotMetricsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var assets = _assets.Assets.Where(x => x.OrganizationId == organizationId).ToList();
        var assignments = _assignments.Assignments.Where(x => x.OrganizationId == organizationId).ToList();
        return Task.FromResult(new DashboardSnapshotMetrics(
            assets.Count,
            assets.Count(x => x.AssignedPersonId is null),
            assignments.Count(x => x.Status is AssignmentStatus.AwaitingAcceptance or AssignmentStatus.Overdue),
            assets.Sum(x => x.PurchasePrice ?? 0m)));
    }
}
