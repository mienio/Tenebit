using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Settings;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class RolePermissionRepository : IRolePermissionRepository
{
    private readonly TenebitDbContext _db;
    public RolePermissionRepository(TenebitDbContext db) => _db = db;

    public async Task<IReadOnlyList<RolePermission>> ListAsync(Guid organizationId, CancellationToken cancellationToken) =>
        await _db.RolePermissions.Where(x => x.OrganizationId == organizationId).ToListAsync(cancellationToken);

    public Task<RolePermission?> FindAsync(Guid organizationId, string roleKey, string permissionKey, CancellationToken cancellationToken) =>
        _db.RolePermissions.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.RoleKey == roleKey && x.PermissionKey == permissionKey, cancellationToken);

    public void Add(RolePermission permission) => _db.RolePermissions.Add(permission);
    public void Remove(RolePermission permission) => _db.RolePermissions.Remove(permission);
}
