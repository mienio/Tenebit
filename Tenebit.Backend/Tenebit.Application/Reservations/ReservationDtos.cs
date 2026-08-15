using Tenebit.Domain.Reservations;

namespace Tenebit.Application.Reservations;

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
