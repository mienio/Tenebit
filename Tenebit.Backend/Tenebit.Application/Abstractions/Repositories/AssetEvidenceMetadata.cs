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

public sealed record AssetEvidenceMetadata(
    Guid Id, Guid AssetId, Guid? AssignmentId, Guid? OffboardingItemId, Guid? AssetAuditItemId, EvidencePhase Phase,
    string FileName, string ContentType, long SizeBytes, string Sha256,
    string? Caption, DateTimeOffset UploadedAt, string UploadedBy,
    EvidenceUploadSource UploadedVia, DateTimeOffset? LockedAt,
    bool LegalHold, DateTimeOffset? RedactedAt);
