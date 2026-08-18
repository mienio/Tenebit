using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Assignments;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class AssignmentRepository : IAssignmentRepository
{
    private readonly TenebitDbContext _db;

    public AssignmentRepository(TenebitDbContext db) => _db = db;

    public async Task<IReadOnlyList<Assignment>> ListAsync(Guid organizationId, CancellationToken cancellationToken) =>
        await BaseQuery(organizationId).OrderByDescending(x => x.IssuedAt).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Assignment>> ListByPersonAsync(Guid organizationId, Guid personId, CancellationToken cancellationToken) =>
        await BaseQuery(organizationId).Where(x => x.PersonId == personId).OrderByDescending(x => x.IssuedAt).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Assignment>> ListByPersonIdsAsync(Guid organizationId, IReadOnlyCollection<Guid> personIds, CancellationToken cancellationToken) =>
        await BaseQuery(organizationId).Where(x => personIds.Contains(x.PersonId)).OrderByDescending(x => x.IssuedAt).ToListAsync(cancellationToken);

    public Task<(IReadOnlyList<Assignment> Items, int Total)> ListPagedAsync(Guid organizationId, string? search, AssignmentStatus? status, int page, int pageSize, CancellationToken cancellationToken) =>
        ListPagedCoreAsync(BaseQuery(organizationId), organizationId, search, status, page, pageSize, cancellationToken);

    public Task<(IReadOnlyList<Assignment> Items, int Total)> ListPagedByPersonIdsAsync(Guid organizationId, string? search, AssignmentStatus? status, int page, int pageSize, IReadOnlyCollection<Guid> personIds, CancellationToken cancellationToken) =>
        ListPagedCoreAsync(BaseQuery(organizationId).Where(x => personIds.Contains(x.PersonId)), organizationId, search, status, page, pageSize, cancellationToken);

    public async Task<IReadOnlyList<Guid>> ListProcedureIdsByPersonIdsAsync(Guid organizationId, IReadOnlyCollection<Guid> personIds, CancellationToken cancellationToken) =>
        await _db.Assignments.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && personIds.Contains(x.PersonId))
            .SelectMany(x => x.ProcedureAcceptances)
            .Select(x => x.ProcedureId)
            .Distinct()
            .ToListAsync(cancellationToken);

    public Task<bool> HasProcedureAssignmentAsync(Guid organizationId, Guid personId, Guid procedureId, CancellationToken cancellationToken) =>
        _db.Assignments.AsNoTracking().AnyAsync(
            x => x.OrganizationId == organizationId && x.PersonId == personId && x.ProcedureAcceptances.Any(a => a.ProcedureId == procedureId),
            cancellationToken);

    public Task<bool> HasProcedureAssignmentForPeopleAsync(Guid organizationId, IReadOnlyCollection<Guid> personIds, Guid procedureId, CancellationToken cancellationToken) =>
        _db.Assignments.AsNoTracking().AnyAsync(
            x => x.OrganizationId == organizationId && personIds.Contains(x.PersonId) && x.ProcedureAcceptances.Any(a => a.ProcedureId == procedureId),
            cancellationToken);

    public Task<Assignment?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        _db.Assignments
            .Include(x => x.Assets)
            .Include(x => x.ProcedureAcceptances)
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, cancellationToken);

    public Task<Assignment?> FindByPublicTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        _db.Assignments
            .Include(x => x.Assets)
            .Include(x => x.ProcedureAcceptances)
            .FirstOrDefaultAsync(x => x.PublicTokenHash == tokenHash, cancellationToken);

    public void Add(Assignment assignment) => _db.Assignments.Add(assignment);

    private IQueryable<Assignment> BaseQuery(Guid organizationId) =>
        _db.Assignments.AsNoTracking()
            .Include(x => x.Assets)
            .Include(x => x.ProcedureAcceptances)
            .Where(x => x.OrganizationId == organizationId);

    private async Task<(IReadOnlyList<Assignment> Items, int Total)> ListPagedCoreAsync(IQueryable<Assignment> query, Guid organizationId, string? search, AssignmentStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x =>
                x.ProtocolNumber.ToLower().Contains(term)
                || _db.People.Any(p => p.OrganizationId == organizationId && p.Id == x.PersonId && (p.FirstName + " " + p.LastName).ToLower().Contains(term))
                || x.Assets.Any(a => _db.Assets.Any(s => s.OrganizationId == organizationId && s.Id == a.AssetId && (s.Name.ToLower().Contains(term) || s.AssetTag.ToLower().Contains(term)))));
        }

        var total = await query.CountAsync(cancellationToken);
        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var items = await query.OrderByDescending(x => x.IssuedAt)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);
        return (items, total);
    }
}
