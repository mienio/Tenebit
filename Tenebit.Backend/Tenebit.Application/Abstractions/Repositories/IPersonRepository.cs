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

public interface IPersonRepository
{
    Task<IReadOnlyList<Person>> ListAsync(Guid organizationId, string? search, CancellationToken cancellationToken);
    Task<IReadOnlyList<Person>> ListScopedAsync(Guid organizationId, string? search, IReadOnlyCollection<Guid> personIds, CancellationToken cancellationToken);
    Task<(IReadOnlyList<Person> Items, int Total)> ListPagedAsync(Guid organizationId, string? search, int page, int pageSize, CancellationToken cancellationToken);
    Task<(IReadOnlyList<Person> Items, int Total)> ListPagedScopedAsync(Guid organizationId, string? search, int page, int pageSize, IReadOnlyCollection<Guid> personIds, CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>> ListManagedScopePersonIdsAsync(Guid organizationId, Guid managerPersonId, IReadOnlyCollection<Guid> managedTeamIds, CancellationToken cancellationToken);
    Task<Person?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    Task<Person?> FindByEmailAsync(Guid organizationId, string email, CancellationToken cancellationToken);
    Task<bool> EmailExistsAsync(Guid organizationId, string email, Guid? excludingPersonId, CancellationToken cancellationToken);
    Task<bool> HasBlockingRelationsAsync(Guid organizationId, Guid personId, CancellationToken cancellationToken);
    Task<int> CountByLocationAsync(Guid organizationId, string location, CancellationToken cancellationToken);
    Task<int> CountByLocationIdAsync(Guid organizationId, Guid locationId, CancellationToken cancellationToken);
    void Add(Person person);
    void Remove(Person person);
}
