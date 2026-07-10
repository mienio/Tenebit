using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Identity;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class OrganizationUserRepository : IOrganizationUserRepository
{
    private readonly TenebitDbContext _db;
    public OrganizationUserRepository(TenebitDbContext db) => _db = db;

    public async Task<IReadOnlyList<OrganizationUser>> ListAsync(Guid organizationId, CancellationToken cancellationToken) =>
        await _db.OrganizationUsers.Include(x => x.Roles).Where(x => x.OrganizationId == organizationId).OrderBy(x => x.Email).ToListAsync(cancellationToken);

    public Task<OrganizationUser?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        _db.OrganizationUsers.Include(x => x.Roles).FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, cancellationToken);

    public Task<OrganizationUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _db.OrganizationUsers.Include(x => x.Roles).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> EmailExistsAsync(Guid organizationId, string email, Guid? excludingId, CancellationToken cancellationToken) =>
        _db.OrganizationUsers.AnyAsync(x => x.OrganizationId == organizationId && x.Email == email.Trim().ToLowerInvariant() && (!excludingId.HasValue || x.Id != excludingId.Value), cancellationToken);

    public Task<OrganizationUser?> FindByEmailAsync(string email, CancellationToken cancellationToken) =>
        _db.OrganizationUsers.Include(x => x.Roles).FirstOrDefaultAsync(x => x.Email == email.Trim().ToLowerInvariant(), cancellationToken);

    public void Add(OrganizationUser user) => _db.OrganizationUsers.Add(user);
}
