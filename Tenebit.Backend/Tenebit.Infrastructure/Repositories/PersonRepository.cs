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
        var query = ApplySearch(_db.People.AsNoTracking().Where(x => x.OrganizationId == organizationId), search);
        return await query.OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Person>> ListScopedAsync(Guid organizationId, string? search, IReadOnlyCollection<Guid> personIds, CancellationToken cancellationToken)
    {
        var query = _db.People.AsNoTracking().Where(x => x.OrganizationId == organizationId && personIds.Contains(x.Id));
        query = ApplySearch(query, search);
        return await query.OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Person> Items, int Total)> ListPagedAsync(Guid organizationId, string? search, string? sortKey, bool sortDesc, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = ApplySearch(_db.People.AsNoTracking().Where(x => x.OrganizationId == organizationId), search);
        return await PageAsync(query, sortKey, sortDesc, page, pageSize, cancellationToken);
    }

    public async Task<(IReadOnlyList<Person> Items, int Total)> ListPagedScopedAsync(Guid organizationId, string? search, string? sortKey, bool sortDesc, int page, int pageSize, IReadOnlyCollection<Guid> personIds, CancellationToken cancellationToken)
    {
        var query = _db.People.AsNoTracking().Where(x => x.OrganizationId == organizationId && personIds.Contains(x.Id));
        query = ApplySearch(query, search);
        return await PageAsync(query, sortKey, sortDesc, page, pageSize, cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> ListManagedScopePersonIdsAsync(Guid organizationId, Guid managerPersonId, IReadOnlyCollection<Guid> managedTeamIds, CancellationToken cancellationToken) =>
        await _db.People.AsNoTracking()
            .Where(person => person.OrganizationId == organizationId &&
                (person.Id == managerPersonId
                 || person.ManagerId == managerPersonId
                 || (person.TeamId.HasValue && managedTeamIds.Contains(person.TeamId.Value))))
            .Select(person => person.Id)
            .ToListAsync(cancellationToken);

    public Task<Person?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        _db.People.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, cancellationToken);

    public Task<Person?> FindByEmailAsync(Guid organizationId, string email, CancellationToken cancellationToken) =>
        _db.People.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Email == email.Trim().ToLower(), cancellationToken);

    public Task<bool> EmailExistsAsync(Guid organizationId, string email, Guid? excludingPersonId, CancellationToken cancellationToken) =>
        _db.People.AnyAsync(x => x.OrganizationId == organizationId && x.Email == email.Trim().ToLower() && (!excludingPersonId.HasValue || x.Id != excludingPersonId.Value), cancellationToken);

    public async Task<bool> HasBlockingRelationsAsync(Guid organizationId, Guid personId, CancellationToken cancellationToken) =>
        await _db.People.AnyAsync(x => x.OrganizationId == organizationId && x.ManagerId == personId, cancellationToken)
        || await _db.Assets.AnyAsync(x => x.OrganizationId == organizationId && x.AssignedPersonId == personId, cancellationToken)
        || await _db.Assignments.AnyAsync(x => x.OrganizationId == organizationId && x.PersonId == personId, cancellationToken)
        || await _db.OrganizationUsers.AnyAsync(x => x.OrganizationId == organizationId && x.PersonId == personId, cancellationToken);

    public Task<int> CountAsync(Guid organizationId, CancellationToken cancellationToken) =>
        _db.People.AsNoTracking().CountAsync(x => x.OrganizationId == organizationId, cancellationToken);

    public Task<int> CountByLocationAsync(Guid organizationId, string location, CancellationToken cancellationToken) =>
        _db.People.AsNoTracking().CountAsync(x => x.OrganizationId == organizationId && x.Location == location, cancellationToken);

    public void Add(Person person) => _db.People.Add(person);
    public void Remove(Person person) => _db.People.Remove(person);

    private static IQueryable<Person> ApplySearch(IQueryable<Person> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        var term = search.Trim().ToLower();
        return query.Where(x => x.FirstName.ToLower().Contains(term) || x.LastName.ToLower().Contains(term) || x.Email.ToLower().Contains(term));
    }

    private static async Task<(IReadOnlyList<Person> Items, int Total)> PageAsync(IQueryable<Person> query, string? sortKey, bool sortDesc, int page, int pageSize, CancellationToken cancellationToken)
    {
        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var total = await query.CountAsync(cancellationToken);
        var items = await ApplySort(query, sortKey, sortDesc)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    // Sorting has to run in the database: the list is paged server-side, so ordering only the current page
    // would silently reorder 25 rows and call it a sorted list.
    private static IOrderedQueryable<Person> ApplySort(IQueryable<Person> query, string? sortKey, bool sortDesc) => sortKey switch
    {
        "email" => sortDesc ? query.OrderByDescending(x => x.Email) : query.OrderBy(x => x.Email),
        "jobTitle" => sortDesc ? query.OrderByDescending(x => x.JobTitle) : query.OrderBy(x => x.JobTitle),
        "relationType" => sortDesc ? query.OrderByDescending(x => x.RelationType) : query.OrderBy(x => x.RelationType),
        "status" => sortDesc
            ? query.OrderByDescending(x => x.IsActive).ThenByDescending(x => x.LastName).ThenByDescending(x => x.FirstName)
            : query.OrderBy(x => x.IsActive).ThenBy(x => x.LastName).ThenBy(x => x.FirstName),
        _ => sortDesc
            ? query.OrderByDescending(x => x.LastName).ThenByDescending(x => x.FirstName)
            : query.OrderBy(x => x.LastName).ThenBy(x => x.FirstName)
    };

    public Task<int> CountByLocationIdAsync(Guid organizationId, Guid locationId, CancellationToken cancellationToken) =>
        _db.People.CountAsync(x => x.OrganizationId == organizationId && x.LocationId == locationId, cancellationToken);
}
