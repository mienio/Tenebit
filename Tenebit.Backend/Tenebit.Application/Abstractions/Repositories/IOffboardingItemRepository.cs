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

public interface IOffboardingItemRepository
{
    Task<IReadOnlyList<OffboardingItem>> ListByCaseAsync(Guid organizationId, Guid offboardingCaseId, CancellationToken cancellationToken);

    /// <summary>Tracked counterpart of <see cref="ListByCaseAsync"/>, for callers that change items or judge the
    /// case status from them. The read-only overload detaches its rows, so it hands back the database state from
    /// before the current operation - status recomputation then lagged one step behind and item changes made on
    /// its rows were never saved.</summary>
    Task<IReadOnlyList<OffboardingItem>> ListByCaseForUpdateAsync(Guid organizationId, Guid offboardingCaseId, CancellationToken cancellationToken);
    Task<OffboardingItem?> GetAsync(Guid organizationId, Guid offboardingCaseId, Guid itemId, CancellationToken cancellationToken);
    void Add(OffboardingItem item);
}
