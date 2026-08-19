using System.Linq.Expressions;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Evidence;

namespace Tenebit.Infrastructure.Repositories;

internal static class AssetEvidenceMetadataProjection
{
    internal static readonly Expression<Func<AssetEvidence, AssetEvidenceMetadata>> Select = x =>
        new AssetEvidenceMetadata(
            x.Id, x.AssetId, x.AssignmentId, x.OffboardingItemId, x.AssetAuditItemId, x.Phase, x.FileName, x.ContentType, x.SizeBytes, x.Sha256,
            x.Caption, x.UploadedAt, x.UploadedBy, x.UploadedVia, x.LockedAt, x.LegalHold, x.RedactedAt);
}
