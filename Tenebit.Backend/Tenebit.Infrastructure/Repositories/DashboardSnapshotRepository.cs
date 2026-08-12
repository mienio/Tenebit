using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Dashboards;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class DashboardSnapshotRepository : IDashboardSnapshotRepository
{
    private readonly TenebitDbContext _db;
    public DashboardSnapshotRepository(TenebitDbContext db) => _db = db;

    public Task<DashboardSnapshot?> GetForDateAsync(Guid organizationId, DateOnly date, CancellationToken cancellationToken) =>
        _db.DashboardSnapshots.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.SnapshotDate == date, cancellationToken);

    public Task<DashboardSnapshot?> GetClosestOnOrBeforeAsync(Guid organizationId, DateOnly onOrBefore, CancellationToken cancellationToken) =>
        _db.DashboardSnapshots
            .Where(x => x.OrganizationId == organizationId && x.SnapshotDate <= onOrBefore)
            .OrderByDescending(x => x.SnapshotDate)
            .FirstOrDefaultAsync(cancellationToken);

    public void Add(DashboardSnapshot snapshot) => _db.DashboardSnapshots.Add(snapshot);
}
