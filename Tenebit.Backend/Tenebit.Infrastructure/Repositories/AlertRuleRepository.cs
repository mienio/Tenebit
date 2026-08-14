using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Alerts;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class AlertRuleRepository : IAlertRuleRepository
{
    private readonly TenebitDbContext _db;

    public AlertRuleRepository(TenebitDbContext db) => _db = db;

    public async Task<IReadOnlyList<AlertRule>> ListByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken) =>
        await _db.AlertRules
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);

    public Task<AlertRule?> GetAsync(Guid organizationId, AlertType type, CancellationToken cancellationToken) =>
        _db.AlertRules
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Type == type, cancellationToken);

    public void Add(AlertRule rule) => _db.AlertRules.Add(rule);

    public void Update(AlertRule rule) => _db.AlertRules.Update(rule);
}
