using Tenebit.Domain.Evidence;

namespace Tenebit.Application.Evidence;

public sealed record AssetEvidenceResponse(
    Guid Id, Guid AssetId, Guid? AssignmentId, EvidencePhase Phase,
    string FileName, string ContentType, long SizeBytes, string Sha256,
    string? Caption, DateTimeOffset UploadedAt, string UploadedBy,
    EvidenceUploadSource UploadedVia, DateTimeOffset? LockedAt,
    bool LegalHold, DateTimeOffset? RedactedAt);

public sealed record UploadAssetEvidenceRequest(EvidencePhase Phase, Guid? AssignmentId, string? Caption);
public sealed record SetEvidenceLegalHoldRequest(bool Enabled);

/// <summary>Pojedynczy plik do zapisania jako materiał dowodowy w ramach transakcji zbiorczej
/// (wydanie ze zdjęciami / zwrot ze zdjęciami).</summary>
public sealed record EvidenceUploadInput(
    Guid AssetId,
    string FileName,
    string? ContentType,
    byte[] Content,
    string? Caption,
    string UploadedBy,
    EvidenceUploadSource UploadedVia);
