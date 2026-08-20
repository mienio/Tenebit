using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Licenses;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class LicenseRepository : ILicenseRepository
{
    private readonly TenebitDbContext _db;
    public LicenseRepository(TenebitDbContext db) => _db = db;

    public async Task<IReadOnlyList<License>> ListAsync(Guid organizationId, CancellationToken cancellationToken) =>
        await _db.Licenses.Include(x => x.Seats).Where(x => x.OrganizationId == organizationId).OrderBy(x => x.Name).ToListAsync(cancellationToken);

    public Task<License?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        _db.Licenses.Include(x => x.Seats).FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, cancellationToken);

    public Task<int> CountAsync(Guid organizationId, CancellationToken cancellationToken) =>
        _db.Licenses.AsNoTracking().CountAsync(x => x.OrganizationId == organizationId, cancellationToken);

    public void Add(License license) => _db.Licenses.Add(license);
    public void Remove(License license) => _db.Licenses.Remove(license);
}
