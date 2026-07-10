using Tenebit.Domain.Assignments;

namespace Tenebit.Application.Workspace;

public sealed record MyAssetResponse(Guid Id, string Name, string AssetTag, string? CategoryName, string? Location, DateOnly? WarrantyUntil);

public sealed record MyProcedureResponse(Guid ProcedureId, string? Title, AcceptanceStatus Status, Guid? DocumentId, string? DocumentFileName);

public sealed record MyAssignmentResponse(Guid Id, string ProtocolNumber, AssignmentStatus Status, DateTimeOffset IssuedAt, DateOnly? DueDate, IReadOnlyList<string> AssetNames, IReadOnlyList<MyProcedureResponse> Procedures);

public sealed record MyWorkspaceResponse(bool HasPersonRecord, string? PersonName, IReadOnlyList<MyAssetResponse> Assets, IReadOnlyList<MyAssignmentResponse> Assignments);
