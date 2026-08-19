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

public interface IOrganizationUserRepository
{
    Task<IReadOnlyList<OrganizationUser>> ListAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<bool> PersonLinkExistsAsync(Guid organizationId, Guid personId, Guid? excludingId, CancellationToken cancellationToken);
    Task<OrganizationUser?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    Task<OrganizationUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<UserSecurityState?> GetSecurityStateAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> TryConsumeTotpCounterAsync(Guid id, long counter, CancellationToken cancellationToken);
    Task<bool> EmailExistsAsync(Guid organizationId, string email, Guid? excludingId, CancellationToken cancellationToken);
    Task<OrganizationUser?> FindByEmailAsync(string email, CancellationToken cancellationToken);
    void Add(OrganizationUser user);
}
