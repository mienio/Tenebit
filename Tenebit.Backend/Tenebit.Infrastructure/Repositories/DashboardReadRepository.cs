using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Assignments;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class DashboardReadRepository : IDashboardReadRepository
{
    private readonly TenebitDbContext _db;

    public DashboardReadRepository(TenebitDbContext db) => _db = db;

    public async Task<DashboardReadModel> GetSummaryAsync(Guid organizationId, DateOnly today, DateOnly warrantyLimit, CancellationToken cancellationToken)
    {
        var assets = _db.Assets.AsNoTracking().Where(x => x.OrganizationId == organizationId);
        var assetMetrics = await assets
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Total = group.Count(),
                InStock = group.Count(x => x.Status == AssetStatus.InStock),
                Assigned = group.Count(x => x.Status == AssetStatus.Assigned),
                InService = group.Count(x => x.Status == AssetStatus.InService),
                WithoutOwner = group.Count(x => x.AssignedPersonId == null),
                Value = group.Sum(x => x.PurchasePrice ?? 0m)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var peopleCount = await _db.People.AsNoTracking().CountAsync(x => x.OrganizationId == organizationId, cancellationToken);

        var openAssignments = await _db.Assignments.AsNoTracking().CountAsync(
            x => x.OrganizationId == organizationId && (x.Status == AssignmentStatus.AwaitingAcceptance || x.Status == AssignmentStatus.Overdue),
            cancellationToken);

        var pendingAcceptances = await _db.Assignments
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .SelectMany(x => x.ProcedureAcceptances)
            .CountAsync(x => x.Status == AcceptanceStatus.Pending, cancellationToken);

        var licenseMetrics = await _db.Licenses
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Total = group.Count(),
                SeatsTotal = group.Sum(x => x.SeatsTotal),
                SeatsUsed = group.Sum(x => x.Seats.Count)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var byStatus = (await assets
            .GroupBy(x => x.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken))
            .Select(x => new DashboardStatusCount(x.Status, x.Count))
            .OrderBy(x => x.Status)
            .ToList();

        var warranty = await assets
            .Where(x => x.WarrantyUntil.HasValue && x.WarrantyUntil.Value >= today && x.WarrantyUntil.Value <= warrantyLimit)
            .OrderBy(x => x.WarrantyUntil)
            .Take(8)
            .Select(x => new DashboardWarrantyAsset(x.Id, x.Name, x.AssetTag, x.WarrantyUntil!.Value))
            .ToListAsync(cancellationToken);

        var byCategory = (await (
                from asset in _db.Assets.AsNoTracking()
                join category in _db.AssetCategories.AsNoTracking()
                    on new { asset.OrganizationId, Id = asset.CategoryId } equals new { category.OrganizationId, category.Id }
                where asset.OrganizationId == organizationId
                group asset by new { category.Id, category.Name } into grouped
                orderby grouped.Count() descending
                select new { grouped.Key.Id, grouped.Key.Name, Count = grouped.Count() })
            .ToListAsync(cancellationToken))
            .Select(x => new DashboardCategoryCount(x.Id, x.Name, x.Count))
            .ToList();

        var byLocation = (await assets
            .Where(x => x.Location != null && x.Location != string.Empty)
            .GroupBy(x => x.Location!)
            .Select(group => new { Location = group.Key, Count = group.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync(cancellationToken))
            .Select(x => new DashboardLocationCount(x.Location, x.Count))
            .ToList();

        var byTeam = (await (
                from asset in _db.Assets.AsNoTracking()
                join team in _db.Teams.AsNoTracking()
                    on new { asset.OrganizationId, Id = asset.TeamId!.Value } equals new { team.OrganizationId, team.Id }
                where asset.OrganizationId == organizationId && asset.TeamId.HasValue
                group asset by new { team.Id, team.Name } into grouped
                orderby grouped.Count() descending
                select new { grouped.Key.Id, grouped.Key.Name, Count = grouped.Count(), Value = grouped.Sum(x => x.PurchasePrice ?? 0m) })
            .ToListAsync(cancellationToken))
            .Select(x => new DashboardTeamCount(x.Id, x.Name, x.Count, x.Value))
            .ToList();

        return new DashboardReadModel(
            assetMetrics?.Total ?? 0,
            assetMetrics?.InStock ?? 0,
            assetMetrics?.Assigned ?? 0,
            assetMetrics?.InService ?? 0,
            assetMetrics?.WithoutOwner ?? 0,
            peopleCount,
            openAssignments,
            pendingAcceptances,
            assetMetrics?.Value ?? 0m,
            licenseMetrics?.Total ?? 0,
            licenseMetrics?.SeatsUsed ?? 0,
            licenseMetrics?.SeatsTotal ?? 0,
            byStatus,
            warranty,
            byCategory,
            byLocation,
            byTeam);
    }

    public async Task<DashboardSnapshotMetrics> GetSnapshotMetricsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var assets = await _db.Assets
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Total = group.Count(),
                WithoutOwner = group.Count(x => x.AssignedPersonId == null),
                Value = group.Sum(x => x.PurchasePrice ?? 0m)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var openAssignments = await _db.Assignments.AsNoTracking().CountAsync(
            x => x.OrganizationId == organizationId && (x.Status == AssignmentStatus.AwaitingAcceptance || x.Status == AssignmentStatus.Overdue),
            cancellationToken);

        return new DashboardSnapshotMetrics(
            assets?.Total ?? 0,
            assets?.WithoutOwner ?? 0,
            openAssignments,
            assets?.Value ?? 0m);
    }
}
