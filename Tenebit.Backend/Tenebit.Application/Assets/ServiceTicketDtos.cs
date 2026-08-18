using Tenebit.Domain.Assets;

namespace Tenebit.Application.Assets;

[ValidatedRequest]
public sealed record OpenServiceTicketRequest(Guid AssetId, Guid? AssetInspectionId, string Vendor, string? Description, decimal? EstimatedCost, string? Currency, DateTimeOffset? SlaDueAt);

[ValidatedRequest]
public sealed record UpdateServiceTicketRequest(string Vendor, string? Description, decimal? EstimatedCost, string? Currency, DateTimeOffset? SlaDueAt);

[ValidatedRequest]
public sealed record CompleteServiceTicketRequest(decimal? ActualCost, string? Resolution, AssetStatus ResultStatus);

[ValidatedRequest]
public sealed record CancelServiceTicketRequest(string? Resolution);

public sealed record ServiceTicketResponse(Guid Id, Guid AssetId, Guid? AssetInspectionId, string Vendor, string? Description, decimal? EstimatedCost, decimal? ActualCost, string? Currency, DateTimeOffset OpenedAt, DateTimeOffset? SlaDueAt, DateTimeOffset? ClosedAt, ServiceTicketStatus Status, string? Resolution);

public sealed record ServiceTicketListResponse(IReadOnlyList<ServiceTicketResponse> Items, int Total);
