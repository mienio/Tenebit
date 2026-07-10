using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Alerts;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class SentAlertRepository : ISentAlertRepository
{
    private readonly TenebitDbContext _db;

    public SentAlertRepository(TenebitDbContext db) => _db = db;

    public Task<bool> ExistsAsync(Guid organizationId, string alertKey, Guid entityId, CancellationToken cancellationToken) =>
        _db.SentAlerts.AnyAsync(x => x.OrganizationId == organizationId && x.AlertKey == alertKey && x.EntityId == entityId, cancellationToken);

    public void Add(SentAlert alert) => _db.SentAlerts.Add(alert);
}
