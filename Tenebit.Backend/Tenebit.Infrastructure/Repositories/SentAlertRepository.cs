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

    public void Add(SentAlert alert) => _db.SentAlerts.Add(alert);
}
