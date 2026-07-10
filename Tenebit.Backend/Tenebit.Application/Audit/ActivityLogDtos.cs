namespace Tenebit.Application.Audit;

public sealed record ActivityLogEntryResponse(Guid Id, string Action, string EntityType, Guid? EntityId, string? Details, string ActorDisplay, DateTimeOffset CreatedAt);
public sealed record PagedActivityLogResponse(IReadOnlyList<ActivityLogEntryResponse> Items, int Total, int Page, int PageSize);
