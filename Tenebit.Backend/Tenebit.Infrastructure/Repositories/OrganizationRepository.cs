using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Organizations;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class OrganizationRepository : IOrganizationRepository
{
    private readonly TenebitDbContext _db;

    public OrganizationRepository(TenebitDbContext db) => _db = db;

    public Task<Organization?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        _db.Organizations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Organization>> ListAllAsync(CancellationToken cancellationToken) =>
        await _db.Organizations.AsNoTracking().ToListAsync(cancellationToken);

    public void Add(Organization organization) => _db.Organizations.Add(organization);
}
