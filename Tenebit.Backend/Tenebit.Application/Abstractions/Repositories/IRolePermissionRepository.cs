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

public interface IRolePermissionRepository
{
    Task<IReadOnlyList<RolePermission>> ListAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<RolePermission?> FindAsync(Guid organizationId, string roleKey, string permissionKey, CancellationToken cancellationToken);
    void Add(RolePermission permission);
    void Remove(RolePermission permission);
}
