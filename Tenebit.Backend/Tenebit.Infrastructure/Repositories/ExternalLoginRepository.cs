using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Identity;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class ExternalLoginRepository : IExternalLoginRepository
{
    private readonly TenebitDbContext _db;
    public ExternalLoginRepository(TenebitDbContext db) => _db = db;

    public async Task<OrganizationUser?> FindLinkedUserAsync(string provider, string providerUserId, CancellationToken cancellationToken)
    {
        var link = await _db.ExternalLogins.FirstOrDefaultAsync(x => x.Provider == provider && x.ProviderUserId == providerUserId, cancellationToken);
        if (link is null) return null;
        return await _db.OrganizationUsers.Include(x => x.Roles).FirstOrDefaultAsync(x => x.Id == link.OrganizationUserId, cancellationToken);
    }

    public Task<bool> ExistsAsync(Guid organizationUserId, string provider, CancellationToken cancellationToken) =>
        _db.ExternalLogins.AnyAsync(x => x.OrganizationUserId == organizationUserId && x.Provider == provider, cancellationToken);

    public async Task<IReadOnlyList<string>> ListProvidersAsync(Guid organizationUserId, CancellationToken cancellationToken) =>
        await _db.ExternalLogins.Where(x => x.OrganizationUserId == organizationUserId).Select(x => x.Provider).ToListAsync(cancellationToken);

    public Task<ExternalLogin?> FindAsync(Guid organizationUserId, string provider, CancellationToken cancellationToken) =>
        _db.ExternalLogins.FirstOrDefaultAsync(x => x.OrganizationUserId == organizationUserId && x.Provider == provider, cancellationToken);

    public void Add(ExternalLogin externalLogin) => _db.ExternalLogins.Add(externalLogin);
    public void Remove(ExternalLogin externalLogin) => _db.ExternalLogins.Remove(externalLogin);
}
