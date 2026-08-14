using Tenebit.Domain.Reservations;

namespace Tenebit.Application.Reservations;

public sealed record CreateReservationRequest(
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string Purpose,
    string? PickupLocation,
    string? Notes,
    IReadOnlyList<ReservationItemRequest> Items);

public sealed record ReservationItemRequest(Guid? CategoryId, Guid? KitDefinitionId, int Quantity);

public sealed record UpdateReservationRequest(
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string Purpose,
    string? PickupLocation,
    string? Notes,
    IReadOnlyList<ReservationItemRequest> Items);

public sealed record ApproveReservationRequest(IReadOnlyList<ReservationAllocationRequest> Allocations);

public sealed record ReservationAllocationRequest(Guid ItemId, Guid AssetId);

public sealed record RejectReservationRequest(string Reason);

public sealed record SubstituteReservationItemRequest(Guid ItemId, Guid NewAssetId, string Reason);

public sealed record CancelReservationRequest(string? Reason);

public sealed record ReservationResponse(
    Guid Id,
    Guid RequesterPersonId,
    EquipmentReservationStatus Status,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string Purpose,
    string? PickupLocation,
    string? Notes,
    DateTimeOffset? RequestedAt,
    DateTimeOffset? ApprovedAt,
    string? ApprovedBy,
    DateTimeOffset? RejectedAt,
    string? RejectedBy,
    string? DecisionNotes,
    DateTimeOffset? CancelledAt,
    string? CancelledBy,
    string? CancellationReason,
    DateTimeOffset CreatedAt);

public sealed record ReservationItemResponse(
    Guid Id,
    Guid RequestedCategoryId,
    int RequestedQuantity,
    Guid? KitDefinitionId,
    Guid? AssetId,
    Guid? OriginalAssetId,
    string? SubstitutionReason,
    EquipmentReservationItemStatus Status);

public sealed record ReservationDetailsResponse(ReservationResponse Reservation, IReadOnlyList<ReservationItemResponse> Items);

/// <summary>Widok kalendarzowy dla administratora (spec 8.7): rezerwacje z przedziałem nachodzącym na from-to,
/// wzbogacone o obliczone flagi konfliktu/terminu.</summary>
public sealed record ReservationCalendarItemResponse(
    Guid Id,
    Guid RequesterPersonId,
    EquipmentReservationStatus Status,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string Purpose,
    string? PickupLocation,
    IReadOnlyList<Guid> AssetIds,
    bool IsConflicting,
    bool IsDueToday,
    bool IsOverdue);

