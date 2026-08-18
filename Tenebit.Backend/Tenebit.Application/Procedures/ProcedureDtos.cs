using Tenebit.Domain.Assignments;
using Tenebit.Domain.Procedures;

namespace Tenebit.Application.Procedures;

public sealed record ProcedureAcceptanceStatusResponse(Guid PersonId, string PersonName, AcceptanceStatus Status, DateTimeOffset SentAt, DateTimeOffset? AcceptedAt, string? ProtocolNumber, string? ConfirmedIp, bool IsIntegrityVerified);

public sealed record ProcedureResponse(
    Guid Id,
    string Title,
    string Version,
    string Owner,
    ProcedureStatus Status,
    string? AppliesTo,
    DateOnly? ReviewDate,
    bool RequiresAcceptance,
    IReadOnlyList<ProcedureDocumentResponse> Documents,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt);

[ValidatedRequest]
public sealed record CreateProcedureRequest(string Title, string Version, string Owner, string? AppliesTo, DateOnly? ReviewDate, bool RequiresAcceptance);
[ValidatedRequest]
public sealed record UpdateProcedureRequest(string Title, string Version, string Owner, string? AppliesTo, DateOnly? ReviewDate, bool RequiresAcceptance);
public sealed record ProcedureDocumentResponse(Guid Id, string FileName, string ContentType, long SizeBytes, DateTimeOffset UploadedAt, string UploadedBy);
