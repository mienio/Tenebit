using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.JobProfiles;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class JobProfileRepository : IJobProfileRepository
{
    private readonly TenebitDbContext _db;
    public JobProfileRepository(TenebitDbContext db) => _db = db;

    public async Task<IReadOnlyList<JobProfile>> ListAsync(Guid organizationId, CancellationToken cancellationToken) =>
        await _db.JobProfiles.Include(x => x.AssetCategories).Include(x => x.Procedures).Where(x => x.OrganizationId == organizationId).OrderBy(x => x.Name).ToListAsync(cancellationToken);

    public Task<JobProfile?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        _db.JobProfiles.Include(x => x.AssetCategories).Include(x => x.Procedures).FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, cancellationToken);

    public Task<bool> NameExistsAsync(Guid organizationId, string name, Guid? excludingId, CancellationToken cancellationToken) =>
        _db.JobProfiles.AnyAsync(x => x.OrganizationId == organizationId && x.Name == name.Trim() && (!excludingId.HasValue || x.Id != excludingId.Value), cancellationToken);

    public void Add(JobProfile profile) => _db.JobProfiles.Add(profile);
    public void Remove(JobProfile profile) => _db.JobProfiles.Remove(profile);
}
