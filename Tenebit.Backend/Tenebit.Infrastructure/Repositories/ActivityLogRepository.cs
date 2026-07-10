using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Audit;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class ActivityLogRepository : IActivityLogRepository
{
    private readonly TenebitDbContext _db;

    public ActivityLogRepository(TenebitDbContext db) => _db = db;

    public async Task<IReadOnlyList<ActivityLog>> ListAsync(Guid organizationId, int limit, CancellationToken cancellationToken) =>
        await _db.ActivityLogs
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(Math.Clamp(limit, 1, 100))
            .ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<ActivityLog> Items, int Total)> ListPagedAsync(Guid organizationId, int page, int pageSize, string? entityType, Guid? entityId, string? search, CancellationToken cancellationToken)
    {
        var query = _db.ActivityLogs.AsNoTracking().Where(x => x.OrganizationId == organizationId);

        if (!string.IsNullOrWhiteSpace(entityType)) query = query.Where(x => x.EntityType == entityType);
        if (entityId.HasValue) query = query.Where(x => x.EntityId == entityId.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x => x.Action.Contains(search) || (x.Details != null && x.Details.Contains(search)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((Math.Max(page, 1) - 1) * Math.Clamp(pageSize, 1, 100))
            .Take(Math.Clamp(pageSize, 1, 100))
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public void Add(ActivityLog log) => _db.ActivityLogs.Add(log);
}
