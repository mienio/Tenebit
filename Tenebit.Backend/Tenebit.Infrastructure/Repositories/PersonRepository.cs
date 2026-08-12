using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.People;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class PersonRepository : IPersonRepository
{
    private readonly TenebitDbContext _db;

    public PersonRepository(TenebitDbContext db) => _db = db;

    public async Task<IReadOnlyList<Person>> ListAsync(Guid organizationId, string? search, CancellationToken cancellationToken)
    {
        var query = _db.People.AsNoTracking().Where(x => x.OrganizationId == organizationId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x => x.FirstName.ToLower().Contains(term) || x.LastName.ToLower().Contains(term) || x.Email.ToLower().Contains(term));
        }

        return await query.OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Person> Items, int Total)> ListPagedAsync(Guid organizationId, string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _db.People.AsNoTracking().Where(x => x.OrganizationId == organizationId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x => x.FirstName.ToLower().Contains(term) || x.LastName.ToLower().Contains(term) || x.Email.ToLower().Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.LastName).ThenBy(x => x.FirstName)
            .Skip((Math.Max(page, 1) - 1) * Math.Clamp(pageSize, 1, 100))
            .Take(Math.Clamp(pageSize, 1, 100))
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task<Person?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        _db.People.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, cancellationToken);

    public Task<Person?> FindByEmailAsync(Guid organizationId, string email, CancellationToken cancellationToken) =>
        _db.People.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Email == email.Trim().ToLower(), cancellationToken);

    public Task<bool> EmailExistsAsync(Guid organizationId, string email, Guid? excludingPersonId, CancellationToken cancellationToken) =>
        _db.People.AnyAsync(x => x.OrganizationId == organizationId && x.Email == email.Trim().ToLower() && (!excludingPersonId.HasValue || x.Id != excludingPersonId.Value), cancellationToken);

    public async Task<bool> HasBlockingRelationsAsync(Guid organizationId, Guid personId, CancellationToken cancellationToken) =>
        await _db.People.AnyAsync(x => x.OrganizationId == organizationId && x.ManagerId == personId, cancellationToken)
        || await _db.Assets.AnyAsync(x => x.OrganizationId == organizationId && x.AssignedPersonId == personId, cancellationToken)
        || await _db.Assignments.AnyAsync(x => x.OrganizationId == organizationId && x.PersonId == personId, cancellationToken);

    public void Add(Person person) => _db.People.Add(person);
    public void Remove(Person person) => _db.People.Remove(person);
}

