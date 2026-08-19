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

public interface IOffboardingCaseRepository
{
    Task<(IReadOnlyList<OffboardingCase> Items, int Total)> ListPagedAsync(Guid organizationId, OffboardingCaseStatus? status, int page, int pageSize, CancellationToken cancellationToken);
    Task<IReadOnlyList<OffboardingCase>> ListOpenAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<OffboardingCase?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    Task<OffboardingCase?> FindOpenByPersonAsync(Guid organizationId, Guid personId, CancellationToken cancellationToken);
    Task<OffboardingCase?> FindByPublicTokenHashAsync(string tokenHash, CancellationToken cancellationToken);
    void Add(OffboardingCase offboardingCase);
}
