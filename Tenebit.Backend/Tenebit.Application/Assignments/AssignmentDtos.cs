using Tenebit.Domain.Assignments;

namespace Tenebit.Application.Assignments;

[ValidatedRequest]
public sealed record AssignmentAssetRequest(Guid AssetId, string? IssueCondition);

[ValidatedRequest]
public sealed record CreateAssignmentRequest(
    Guid PersonId,
    IReadOnlyList<AssignmentAssetRequest> Assets,
    IReadOnlyList<Guid> ProcedureIds,
    DateOnly? DueDate,
    string? Notes);

[ValidatedRequest]
public sealed record ReturnAssignmentAssetRequest(Guid AssetId, string? ReturnCondition);
[ValidatedRequest]
public sealed record ReturnAssignmentRequest(string? ReturnCondition, string? DestinationLocation, IReadOnlyList<ReturnAssignmentAssetRequest>? Assets = null);
[ValidatedRequest]
public sealed record ReturnAssignmentAssetItemRequest(ReturnResolution Resolution, string? ReturnCondition, string? ReturnLocation, string? Notes);

// Multipart "evidenceManifest": mapuje nazwę części pliku na aktywo i podpis zdjęcia.
[ValidatedRequest]
public sealed record EvidenceManifestEntry(Guid AssetId, string? Caption);

// Multipart "files": pojedynczy plik wraz z nazwą części formularza, do której należy.
public sealed record EvidenceFileInput(string FieldName, string FileName, string? ContentType, byte[] Content);
public sealed record AssignmentAssetResponse(Guid AssetId, string? AssetName, string? AssetTag, string IssueCondition, string? ReturnCondition, DateTimeOffset? ReturnedAt, string? ReturnLocation, string? ReturnedBy, ReturnResolution? ReturnResolution, string? ReturnNotes);
public sealed record ProcedureAcceptanceResponse(Guid Id, Guid ProcedureId, string? ProcedureTitle, AcceptanceStatus Status, DateTimeOffset SentAt, DateTimeOffset? AcceptedAt, string? ConfirmedIp, string? ConfirmationHash, bool IsIntegrityVerified);

public sealed record AssignmentResponse(
    Guid Id,
    Guid PersonId,
    string? PersonName,
    AssignmentStatus Status,
    DateTimeOffset IssuedAt,
    DateOnly? DueDate,
    DateTimeOffset? AcceptedAt,
    DateTimeOffset? ReturnedAt,
    string ProtocolNumber,
    string? Notes,
    IReadOnlyList<AssignmentAssetResponse> Assets,
    IReadOnlyList<ProcedureAcceptanceResponse> ProcedureAcceptances,
    string? AcceptedIp,
    string? AcceptanceHash,
    bool IsIntegrityVerified);

public sealed record AssignmentAcceptanceLinkResponse(string Link);


public sealed record PublicAssignmentAssetResponse(string Name, string AssetTag, string IssueCondition, Guid AssetId, IReadOnlyList<Guid> EvidenceIds);

public sealed record PublicAssignmentDocumentResponse(Guid Id, string FileName);

public sealed record PublicAssignmentProcedureResponse(Guid Id, string Title, string Version, IReadOnlyList<PublicAssignmentDocumentResponse> Documents);

public sealed record PublicAssignmentResponse(
    string OrganizationName,
    string ProtocolNumber,
    AssignmentStatus Status,
    string PersonFirstName,
    IReadOnlyList<PublicAssignmentAssetResponse> Assets,
    IReadOnlyList<PublicAssignmentProcedureResponse> ProceduresRequiringAcceptance);
