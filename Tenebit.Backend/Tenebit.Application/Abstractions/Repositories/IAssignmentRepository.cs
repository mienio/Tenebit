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

public interface IAssignmentRepository
{
    Task<IReadOnlyList<Assignment>> ListAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Assignment>> ListByPersonAsync(Guid organizationId, Guid personId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Assignment>> ListByPersonIdsAsync(Guid organizationId, IReadOnlyCollection<Guid> personIds, CancellationToken cancellationToken);
    Task<(IReadOnlyList<Assignment> Items, int Total)> ListPagedAsync(Guid organizationId, string? search, AssignmentStatus? status, int page, int pageSize, CancellationToken cancellationToken);
    Task<(IReadOnlyList<Assignment> Items, int Total)> ListPagedByPersonIdsAsync(Guid organizationId, string? search, AssignmentStatus? status, int page, int pageSize, IReadOnlyCollection<Guid> personIds, CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>> ListProcedureIdsByPersonIdsAsync(Guid organizationId, IReadOnlyCollection<Guid> personIds, CancellationToken cancellationToken);
    Task<bool> HasProcedureAssignmentAsync(Guid organizationId, Guid personId, Guid procedureId, CancellationToken cancellationToken);
    Task<bool> HasProcedureAssignmentForPeopleAsync(Guid organizationId, IReadOnlyCollection<Guid> personIds, Guid procedureId, CancellationToken cancellationToken);
    Task<Assignment?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    Task<Assignment?> FindByPublicTokenHashAsync(string tokenHash, CancellationToken cancellationToken);
    void Add(Assignment assignment);
}
