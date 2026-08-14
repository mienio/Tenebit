using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Alerts;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class AlertDigestSettingsRepository : IAlertDigestSettingsRepository
{
    private readonly TenebitDbContext _db;

    public AlertDigestSettingsRepository(TenebitDbContext db) => _db = db;

    public Task<AlertDigestSettings?> GetAsync(Guid organizationId, CancellationToken cancellationToken) =>
        _db.AlertDigestSettings.FirstOrDefaultAsync(x => x.OrganizationId == organizationId, cancellationToken);

    public void Add(AlertDigestSettings settings) => _db.AlertDigestSettings.Add(settings);
}
