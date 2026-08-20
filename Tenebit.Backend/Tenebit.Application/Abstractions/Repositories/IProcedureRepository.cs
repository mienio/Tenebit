using Tenebit.Application.Common;
using Tenebit.Domain.Alerts;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Assignments;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Audits;
using Tenebit.Domain.Dashboards;
using Tenebit.Domain.Evidence;
using Tenebit.Domain.Identity;
using Tenebit.Domain.JobProfiles;
using Tenebit.Domain.Licenses;
using Tenebit.Domain.Offboarding;
using Tenebit.Domain.Organizations;
using Tenebit.Domain.People;
using Tenebit.Domain.Procedures;
using Tenebit.Domain.Reservations;
using Tenebit.Domain.Settings;
using Tenebit.Domain.Subscriptions;

namespace Tenebit.Application.Abstractions;

public interface IProcedureRepository
{
    Task<IReadOnlyList<Procedure>> ListAsync(Guid organizationId, string? search, CancellationToken cancellationToken);
    Task<int> CountAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<(IReadOnlyList<Procedure> Items, int Total)> ListPagedAsync(Guid organizationId, string? search, int page, int pageSize, CancellationToken cancellationToken);
    Task<IReadOnlyList<Procedure>> GetByIdsAsync(Guid organizationId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
    Task<Procedure?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProcedureDocumentMetadata>> ListDocumentMetadataByProcedureIdsAsync(Guid organizationId, IReadOnlyCollection<Guid> procedureIds, CancellationToken cancellationToken);
    Task<ProcedureDocumentMetadata?> GetDocumentMetadataAsync(Guid organizationId, Guid procedureId, Guid documentId, CancellationToken cancellationToken);
    Task<bool> HasDocumentsAsync(Guid organizationId, Guid procedureId, CancellationToken cancellationToken);
    Task<ProcedureDocument?> GetDocumentAsync(Guid organizationId, Guid procedureId, Guid documentId, CancellationToken cancellationToken);
    Task<bool> DeleteDocumentAsync(Guid organizationId, Guid procedureId, Guid documentId, CancellationToken cancellationToken);
    void Add(Procedure procedure);
    void AddDocument(ProcedureDocument document);
}
