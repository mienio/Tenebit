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
        await _db.Assignments
            .AsNoTracking()
            .Include(x => x.Assets)
            .Include(x => x.ProcedureAcceptances)
            .Where(x => x.OrganizationId == organizationId)
            .OrderByDescending(x => x.IssuedAt)
            .ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<Assignment> Items, int Total)> ListPagedAsync(Guid organizationId, string? search, AssignmentStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _db.Assignments
            .AsNoTracking()
            .Include(x => x.Assets)
            .Include(x => x.ProcedureAcceptances)
            .Where(x => x.OrganizationId == organizationId);

        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x =>
                x.ProtocolNumber.ToLower().Contains(term)
                || _db.People.Any(p => p.Id == x.PersonId && (p.FirstName + " " + p.LastName).ToLower().Contains(term))
                || x.Assets.Any(a => _db.Assets.Any(s => s.Id == a.AssetId && (s.Name.ToLower().Contains(term) || s.AssetTag.ToLower().Contains(term)))));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.IssuedAt)
            .Skip((Math.Max(page, 1) - 1) * Math.Clamp(pageSize, 1, 100))
            .Take(Math.Clamp(pageSize, 1, 100))
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task<Assignment?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        _db.Assignments
            .Include(x => x.Assets)
            .Include(x => x.ProcedureAcceptances)
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, cancellationToken);

    public void Add(Assignment assignment) => _db.Assignments.Add(assignment);
}
