using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.People;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class PersonRelationTypeRepository : IPersonRelationTypeRepository
{
    private readonly TenebitDbContext _db;
    public PersonRelationTypeRepository(TenebitDbContext db) => _db = db;

    public async Task<IReadOnlyList<PersonRelationType>> ListAsync(Guid organizationId, CancellationToken cancellationToken) =>
        await _db.PersonRelationTypes.Where(x => x.OrganizationId == organizationId).OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync(cancellationToken);

    public Task<PersonRelationType?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        _db.PersonRelationTypes.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, cancellationToken);

    public Task<bool> NameExistsAsync(Guid organizationId, string name, Guid? excludingId, CancellationToken cancellationToken) =>
        _db.PersonRelationTypes.AnyAsync(x => x.OrganizationId == organizationId && x.Name == name.Trim() && (!excludingId.HasValue || x.Id != excludingId.Value), cancellationToken);

    public Task<bool> IsUsedAsync(Guid organizationId, string name, CancellationToken cancellationToken) =>
        _db.People.AnyAsync(x => x.OrganizationId == organizationId && x.RelationType == name, cancellationToken);

    public void Add(PersonRelationType relationType) => _db.PersonRelationTypes.Add(relationType);
    public void Remove(PersonRelationType relationType) => _db.PersonRelationTypes.Remove(relationType);
}
