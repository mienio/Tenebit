using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Alerts;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class SentAlertRepository : ISentAlertRepository
{
    private readonly TenebitDbContext _db;

    public SentAlertRepository(TenebitDbContext db) => _db = db;

    public Task<SentAlert?> GetAsync(Guid organizationId, string alertKey, Guid entityId, string recipientEmail, CancellationToken cancellationToken)
    {
        var normalized = recipientEmail.Trim().ToLowerInvariant();
        return _db.SentAlerts.FirstOrDefaultAsync(x =>
            x.OrganizationId == organizationId && x.AlertKey == alertKey && x.EntityId == entityId && x.RecipientEmail == normalized,
            cancellationToken);
    }

    public Task<SentAlert?> GetLatestAsync(Guid organizationId, Guid entityId, string alertKeyPrefix, CancellationToken cancellationToken) =>
        _db.SentAlerts
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.EntityId == entityId && x.AlertKey.StartsWith(alertKeyPrefix))
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public void Add(SentAlert alert) => _db.SentAlerts.Add(alert);

    public async Task<(IReadOnlyList<SentAlert> Items, int Total)> ListPagedAsync(Guid organizationId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _db.SentAlerts
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .OrderByDescending(x => x.CreatedAt);

        var total = await query.CountAsync(cancellationToken);
        // Same hard cap every other paged repository applies - a caller asking for a million rows gets a
        // page, not the whole table.
        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var items = await query.Skip((safePage - 1) * safePageSize).Take(safePageSize).ToListAsync(cancellationToken);
        return (items, total);
    }
}
