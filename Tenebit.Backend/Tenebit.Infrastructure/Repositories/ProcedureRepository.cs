using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Procedures;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class ProcedureRepository : IProcedureRepository
{
    private readonly TenebitDbContext _db;
    public ProcedureRepository(TenebitDbContext db) => _db = db;

    public async Task<IReadOnlyList<Procedure>> ListAsync(Guid organizationId, string? search, CancellationToken cancellationToken)
    {
        var query = _db.Procedures.Include(x => x.Documents).Where(x => x.OrganizationId == organizationId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var phrase = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.Title.ToLower().Contains(phrase) || x.Owner.ToLower().Contains(phrase));
        }
        return await query.OrderBy(x => x.Title).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Procedure>> GetByIdsAsync(Guid organizationId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await _db.Procedures.Include(x => x.Documents).Where(x => x.OrganizationId == organizationId && ids.Contains(x.Id)).ToListAsync(cancellationToken);

    public Task<Procedure?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        _db.Procedures.Include(x => x.Documents).FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, cancellationToken);

    public Task<ProcedureDocument?> GetDocumentAsync(Guid organizationId, Guid procedureId, Guid documentId, CancellationToken cancellationToken) =>
        _db.ProcedureDocuments.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.ProcedureId == procedureId && x.Id == documentId, cancellationToken);

    public void Add(Procedure procedure) => _db.Procedures.Add(procedure);

    public void AddDocument(ProcedureDocument document) => _db.ProcedureDocuments.Add(document);

    public void RemoveDocument(ProcedureDocument document) => _db.ProcedureDocuments.Remove(document);
}
