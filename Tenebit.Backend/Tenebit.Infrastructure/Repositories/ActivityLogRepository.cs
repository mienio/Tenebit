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

    public async Task<(IReadOnlyList<ActivityLog> Items, int Total)> ListPagedAsync(Guid organizationId, int page, int pageSize, string? entityType, Guid? entityId, string? search, DateTimeOffset? from, DateTimeOffset? to, IReadOnlyCollection<string>? actorSubjects, string? action, CancellationToken cancellationToken)
    {
        var query = _db.ActivityLogs.AsNoTracking().Where(x => x.OrganizationId == organizationId);

        if (!string.IsNullOrWhiteSpace(entityType)) query = query.Where(x => x.EntityType == entityType);
        if (entityId.HasValue) query = query.Where(x => x.EntityId == entityId.Value);
        if (from.HasValue) query = query.Where(x => x.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(x => x.CreatedAt < to.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x => x.Action.Contains(search) || (x.Details != null && x.Details.Contains(search)));
        }

        if (actorSubjects is not null) query = query.Where(x => actorSubjects.Contains(x.ActorSubject));
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(x => x.Action.Contains(action));

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((Math.Max(page, 1) - 1) * Math.Clamp(pageSize, 1, 100))
            .Take(Math.Clamp(pageSize, 1, 100))
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<bool> ExistsRecentAsync(Guid organizationId, string entityType, Guid entityId, string actorSubject, string action, DateTimeOffset since, CancellationToken cancellationToken) =>
        _db.ActivityLogs.AsNoTracking().AnyAsync(x =>
            x.OrganizationId == organizationId &&
            x.EntityType == entityType &&
            x.EntityId == entityId &&
            x.ActorSubject == actorSubject &&
            x.Action == action &&
            x.CreatedAt >= since, cancellationToken);

    public async Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, int batchSize, CancellationToken cancellationToken)
    {
        var ids = await _db.ActivityLogs
            .AsNoTracking()
            .Where(x => x.CreatedAt < cutoff)
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.Id)
            .Take(Math.Clamp(batchSize, 1, 5_000))
            .ToListAsync(cancellationToken);

        if (ids.Count == 0) return 0;
        return await _db.ActivityLogs
            .Where(x => ids.Contains(x.Id) && x.CreatedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public void Add(ActivityLog log) => _db.ActivityLogs.Add(log);
}
