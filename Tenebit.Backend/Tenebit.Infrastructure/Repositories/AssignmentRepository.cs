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

    public Task<Assignment?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        _db.Assignments
            .Include(x => x.Assets)
            .Include(x => x.ProcedureAcceptances)
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, cancellationToken);

    public void Add(Assignment assignment) => _db.Assignments.Add(assignment);
}
