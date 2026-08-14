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

    public void Allocate(Guid assetId)
    {
        if (Status != EquipmentReservationItemStatus.Requested)
        {
            throw new DomainException("Przydzielić można tylko pozycję oczekującą.");
        }

        AssetId = assetId;
        Status = EquipmentReservationItemStatus.Allocated;
    }

    public void Substitute(Guid newAssetId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("Powód zamiany jest wymagany.");
        }

        if (AssetId is null)
        {
            throw new DomainException("Nie można zamienić pozycji bez przydzielonego aktywa.");
        }

        if (Status is EquipmentReservationItemStatus.CheckedOut or EquipmentReservationItemStatus.Returned)
        {
            throw new DomainException("Pozycja jest już rozliczona i nie można jej zamienić.");
        }

        OriginalAssetId = AssetId;
        AssetId = newAssetId;
        SubstitutionReason = reason.Trim();
    }

    public void Reject(string? reason)
    {
        if (Status is EquipmentReservationItemStatus.CheckedOut or EquipmentReservationItemStatus.Returned or EquipmentReservationItemStatus.Rejected)
        {
            throw new DomainException("Pozycja jest już rozliczona.");
        }

        Status = EquipmentReservationItemStatus.Rejected;
        SubstitutionReason = string.IsNullOrWhiteSpace(reason) ? SubstitutionReason : reason.Trim();
    }

    public void MarkCheckedOut()
    {
        if (AssetId is null)
        {
            throw new DomainException("Nie można wydać pozycji bez przydzielonego aktywa.");
        }

        if (Status is EquipmentReservationItemStatus.Rejected or EquipmentReservationItemStatus.Returned)
        {
            throw new DomainException("Pozycja jest już rozliczona.");
        }

        Status = EquipmentReservationItemStatus.CheckedOut;
    }

    public void MarkReturned()
    {
        Status = EquipmentReservationItemStatus.Returned;
    }
}
