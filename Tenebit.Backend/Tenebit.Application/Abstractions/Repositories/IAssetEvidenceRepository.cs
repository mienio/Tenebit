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

public interface IAssetEvidenceRepository
{
    // Full entities are reserved for workflows that really need the blob, such as download or return evidence review.
    Task<IReadOnlyList<AssetEvidence>> ListContentByOffboardingItemAsync(Guid organizationId, Guid offboardingItemId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AssetEvidenceMetadata>> ListMetadataByAssetAsync(Guid organizationId, Guid assetId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AssetEvidenceMetadata>> ListMetadataByAssignmentIdsAsync(Guid organizationId, IReadOnlyCollection<Guid> assignmentIds, CancellationToken cancellationToken);
    Task<IReadOnlyList<AssetEvidenceMetadata>> ListMetadataByAssignmentAsync(Guid organizationId, Guid assignmentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AssetEvidenceRetentionCandidate>> ListRetentionCandidatesAsync(Guid organizationId, DateTimeOffset cutoff, int batchSize, CancellationToken cancellationToken);
    Task<int> RedactAsync(Guid organizationId, IReadOnlyCollection<Guid> evidenceIds, DateTimeOffset redactedAt, CancellationToken cancellationToken);
    Task<AssetEvidence?> GetAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    Task<int> CountAsync(Guid organizationId, Guid assetId, EvidencePhase phase, CancellationToken cancellationToken);
    void Add(AssetEvidence evidence);
    void Remove(AssetEvidence evidence);
}
