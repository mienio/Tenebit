namespace Tenebit.Domain.Reservations;

public enum EquipmentReservationItemStatus
{
    Requested = 0,
    Allocated = 1,
    Approved = 2,
    Rejected = 3,
    Substituted = 4,
    CheckedOut = 5,
    Returned = 6
}
