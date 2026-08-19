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

public interface IAssetAuditParticipantRepository
{
    Task<IReadOnlyList<AssetAuditParticipant>> ListByCampaignAsync(Guid organizationId, Guid campaignId, CancellationToken cancellationToken);
    Task<AssetAuditParticipant?> GetAsync(Guid organizationId, Guid campaignId, Guid participantId, CancellationToken cancellationToken);
    Task<AssetAuditParticipant?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);
    void Add(AssetAuditParticipant participant);
}
