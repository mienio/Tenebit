using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Assets;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class LocationRepository : ILocationRepository
{
    private readonly TenebitDbContext _db;
    public LocationRepository(TenebitDbContext db) => _db = db;

    public async Task<IReadOnlyList<Location>> ListAsync(Guid organizationId, CancellationToken cancellationToken) =>
        await _db.Locations.Where(x => x.OrganizationId == organizationId).OrderBy(x => x.CreatedAt).ThenBy(x => x.Name).ToListAsync(cancellationToken);

    public Task<Location?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        _db.Locations.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, cancellationToken);

    public Task<int> CountAsync(Guid organizationId, CancellationToken cancellationToken) =>
        _db.Locations.AsNoTracking().CountAsync(x => x.OrganizationId == organizationId, cancellationToken);

    public void Add(Location location) => _db.Locations.Add(location);
    public void Remove(Location location) => _db.Locations.Remove(location);
}
