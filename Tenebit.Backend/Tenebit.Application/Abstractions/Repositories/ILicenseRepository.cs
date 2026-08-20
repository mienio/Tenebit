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

public interface ILicenseRepository
{
    Task<IReadOnlyList<License>> ListAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<License?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    Task<int> CountAsync(Guid organizationId, CancellationToken cancellationToken);
    void Add(License license);
    void Remove(License license);
}
