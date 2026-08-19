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

public interface ITwoFactorRecoveryCodeRepository
{
    Task<IReadOnlyList<TwoFactorRecoveryCode>> ListAsync(Guid organizationUserId, CancellationToken cancellationToken);
    Task<bool> TryConsumeAsync(Guid organizationUserId, string codeHash, DateTimeOffset now, CancellationToken cancellationToken);
    void AddRange(IEnumerable<TwoFactorRecoveryCode> codes);
    void RemoveAll(IEnumerable<TwoFactorRecoveryCode> codes);
}
