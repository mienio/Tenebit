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

    public Task<bool> PersonLinkExistsAsync(Guid organizationId, Guid personId, Guid? excludingId, CancellationToken cancellationToken) =>
        _db.OrganizationUsers.AnyAsync(x => x.OrganizationId == organizationId && x.PersonId == personId && (!excludingId.HasValue || x.Id != excludingId.Value), cancellationToken);

    public Task<OrganizationUser?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        _db.OrganizationUsers.Include(x => x.Roles).FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, cancellationToken);

    public Task<OrganizationUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _db.OrganizationUsers.Include(x => x.Roles).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<UserSecurityState?> GetSecurityStateAsync(Guid id, CancellationToken cancellationToken) =>
        _db.OrganizationUsers.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new UserSecurityState(x.OrganizationId, x.SecurityStamp, x.IsActive, x.IsEmailVerified))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> TryConsumeTotpCounterAsync(Guid id, long counter, CancellationToken cancellationToken) =>
        await _db.OrganizationUsers
            .Where(x => x.Id == id && (!x.LastUsedTotpCounter.HasValue || x.LastUsedTotpCounter.Value < counter))
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.LastUsedTotpCounter, counter), cancellationToken) == 1;

    public Task<bool> EmailExistsAsync(Guid organizationId, string email, Guid? excludingId, CancellationToken cancellationToken) =>
        _db.OrganizationUsers.AnyAsync(x => x.OrganizationId == organizationId && x.Email == email.Trim().ToLowerInvariant() && (!excludingId.HasValue || x.Id != excludingId.Value), cancellationToken);

    public Task<OrganizationUser?> FindByEmailAsync(string email, CancellationToken cancellationToken) =>
        _db.OrganizationUsers.Include(x => x.Roles).FirstOrDefaultAsync(x => x.Email == email.Trim().ToLowerInvariant(), cancellationToken);

    public void Add(OrganizationUser user) => _db.OrganizationUsers.Add(user);
}
