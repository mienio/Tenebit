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
        var query = BaseQuery(organizationId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var phrase = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.Title.ToLower().Contains(phrase)
                || x.Owner.ToLower().Contains(phrase)
                || x.Version.ToLower().Contains(phrase));
        }

        return await query.OrderBy(x => x.Title).ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Procedure> Items, int Total)> ListPagedAsync(Guid organizationId, string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = BaseQuery(organizationId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var phrase = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.Title.ToLower().Contains(phrase)
                || x.Owner.ToLower().Contains(phrase)
                || x.Version.ToLower().Contains(phrase));
        }

        var normalizedPage = Math.Max(page, 1);
        var normalizedPageSize = Math.Clamp(pageSize, 1, 100);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.Title)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<IReadOnlyList<Procedure>> GetByIdsAsync(Guid organizationId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0) return [];
        return await BaseQuery(organizationId)
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);
    }

    public Task<Procedure?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        _db.Procedures.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ProcedureDocumentMetadata>> ListDocumentMetadataByProcedureIdsAsync(Guid organizationId, IReadOnlyCollection<Guid> procedureIds, CancellationToken cancellationToken)
    {
        if (procedureIds.Count == 0) return [];

        return await _db.ProcedureDocuments
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && procedureIds.Contains(x.ProcedureId))
            .OrderByDescending(x => x.UploadedAt)
            .Select(x => new ProcedureDocumentMetadata(x.Id, x.ProcedureId, x.FileName, x.ContentType, x.SizeBytes, x.UploadedAt, x.UploadedBy))
            .ToListAsync(cancellationToken);
    }

    public Task<ProcedureDocumentMetadata?> GetDocumentMetadataAsync(Guid organizationId, Guid procedureId, Guid documentId, CancellationToken cancellationToken) =>
        _db.ProcedureDocuments
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.ProcedureId == procedureId && x.Id == documentId)
            .Select(x => new ProcedureDocumentMetadata(x.Id, x.ProcedureId, x.FileName, x.ContentType, x.SizeBytes, x.UploadedAt, x.UploadedBy))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> HasDocumentsAsync(Guid organizationId, Guid procedureId, CancellationToken cancellationToken) =>
        _db.ProcedureDocuments.AsNoTracking().AnyAsync(x => x.OrganizationId == organizationId && x.ProcedureId == procedureId, cancellationToken);

    public Task<ProcedureDocument?> GetDocumentAsync(Guid organizationId, Guid procedureId, Guid documentId, CancellationToken cancellationToken) =>
        _db.ProcedureDocuments.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.ProcedureId == procedureId && x.Id == documentId, cancellationToken);

    public async Task<bool> DeleteDocumentAsync(Guid organizationId, Guid procedureId, Guid documentId, CancellationToken cancellationToken) =>
        await _db.ProcedureDocuments
            .Where(x => x.OrganizationId == organizationId && x.ProcedureId == procedureId && x.Id == documentId)
            .ExecuteDeleteAsync(cancellationToken) == 1;

    public void Add(Procedure procedure) => _db.Procedures.Add(procedure);

    public void AddDocument(ProcedureDocument document) => _db.ProcedureDocuments.Add(document);

    private IQueryable<Procedure> BaseQuery(Guid organizationId) =>
        _db.Procedures.AsNoTracking().Where(x => x.OrganizationId == organizationId);
}
