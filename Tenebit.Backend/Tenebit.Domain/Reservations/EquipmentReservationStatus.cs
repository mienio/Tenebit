namespace Tenebit.Domain.Reservations;

public enum EquipmentReservationStatus
{
    Draft = 0,
    PendingApproval = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4,
    ReadyForPickup = 5,
    CheckedOut = 6,
    Completed = 7,
    Expired = 8
}
