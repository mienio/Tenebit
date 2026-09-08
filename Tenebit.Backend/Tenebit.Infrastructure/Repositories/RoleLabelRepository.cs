using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Settings;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class RoleLabelRepository : IRoleLabelRepository
{
    private readonly TenebitDbContext _db;
    public RoleLabelRepository(TenebitDbContext db) => _db = db;

    public async Task<IReadOnlyList<RoleLabel>> ListAsync(Guid organizationId, CancellationToken cancellationToken) =>
        await _db.RoleLabels.Where(x => x.OrganizationId == organizationId).ToListAsync(cancellationToken);

    public Task<RoleLabel?> FindAsync(Guid organizationId, string roleKey, CancellationToken cancellationToken) =>
        _db.RoleLabels.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.RoleKey == roleKey, cancellationToken);

    public void Add(RoleLabel label) => _db.RoleLabels.Add(label);
}
