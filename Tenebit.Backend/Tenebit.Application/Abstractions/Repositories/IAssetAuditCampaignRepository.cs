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

public interface IAssetAuditCampaignRepository
{
    Task<(IReadOnlyList<AssetAuditCampaign> Items, int Total)> ListPagedAsync(Guid organizationId, AssetAuditCampaignStatus? status, int page, int pageSize, CancellationToken cancellationToken);
    Task<IReadOnlyList<AssetAuditCampaign>> ListActiveAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<AssetAuditCampaign?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    void Add(AssetAuditCampaign campaign);
}
