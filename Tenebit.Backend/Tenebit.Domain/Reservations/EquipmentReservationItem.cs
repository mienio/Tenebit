using Tenebit.Domain.Common;

namespace Tenebit.Domain.Reservations;

public sealed class EquipmentReservationItem
{
    private EquipmentReservationItem() { }

    public EquipmentReservationItem(Guid organizationId, Guid reservationId, Guid requestedCategoryId, int requestedQuantity, Guid? kitDefinitionId)
    {
        if (requestedQuantity <= 0)
        {
            throw new DomainException("Wymagana ilość musi być większa od zera.");
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        ReservationId = reservationId;
        RequestedCategoryId = requestedCategoryId;
        RequestedQuantity = requestedQuantity;
        KitDefinitionId = kitDefinitionId;
        Status = EquipmentReservationItemStatus.Requested;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ReservationId { get; private set; }
    public Guid RequestedCategoryId { get; private set; }
    public int RequestedQuantity { get; private set; }
    public Guid? KitDefinitionId { get; private set; }
    public Guid? AssetId { get; private set; }
    public Guid? OriginalAssetId { get; private set; }
    public string? SubstitutionReason { get; private set; }
    public EquipmentReservationItemStatus Status { get; private set; }
}
