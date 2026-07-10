using Tenebit.Domain.Assignments;

namespace Tenebit.Application.Assignments;

public sealed record AssignmentAssetRequest(Guid AssetId, string? IssueCondition);

public sealed record CreateAssignmentRequest(
    Guid PersonId,
    IReadOnlyList<AssignmentAssetRequest> Assets,
    IReadOnlyList<Guid> ProcedureIds,
    DateOnly? DueDate,
    string? Notes);

public sealed record ReturnAssignmentRequest(string? ReturnCondition, string? DestinationLocation);
public sealed record AssignmentAssetResponse(Guid AssetId, string? AssetName, string? AssetTag, string IssueCondition, string? ReturnCondition);
public sealed record ProcedureAcceptanceResponse(Guid Id, Guid ProcedureId, string? ProcedureTitle, AcceptanceStatus Status, DateTimeOffset SentAt, DateTimeOffset? AcceptedAt);

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
    IReadOnlyList<ProcedureAcceptanceResponse> ProcedureAcceptances);

public sealed record PublicAssignmentAssetResponse(string Name, string AssetTag, string IssueCondition);

public sealed record PublicAssignmentResponse(
    string OrganizationName,
    string ProtocolNumber,
    AssignmentStatus Status,
    string PersonFirstName,
    IReadOnlyList<PublicAssignmentAssetResponse> Assets,
    IReadOnlyList<string> ProcedureTitlesRequiringAcceptance);
