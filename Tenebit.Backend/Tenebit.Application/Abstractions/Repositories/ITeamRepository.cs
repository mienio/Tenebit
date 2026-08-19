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

public interface ITeamRepository
{
    Task<IReadOnlyList<Team>> ListAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>> ListManagedIdsAsync(Guid organizationId, Guid managerPersonId, CancellationToken cancellationToken);
    Task<Team?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    Task<bool> NameExistsAsync(Guid organizationId, string name, Guid? excludingTeamId, CancellationToken cancellationToken);
    Task<bool> IsUsedAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    void Add(Team team);
    void Remove(Team team);
}
