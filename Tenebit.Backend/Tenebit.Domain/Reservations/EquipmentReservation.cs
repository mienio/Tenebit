using Tenebit.Domain.Common;

namespace Tenebit.Domain.Reservations;

public sealed class EquipmentReservation
{
    private EquipmentReservation() { }

    public EquipmentReservation(Guid organizationId, Guid requesterPersonId, DateTimeOffset startAt, DateTimeOffset endAt, string purpose, string? pickupLocation, string? notes)
    {
        if (string.IsNullOrWhiteSpace(purpose))
        {
            throw new DomainException("Cel rezerwacji jest wymagany.");
        }

        if (endAt <= startAt)
        {
            throw new DomainException("Data zakończenia musi być późniejsza niż data rozpoczęcia.");
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        RequesterPersonId = requesterPersonId;
        Status = EquipmentReservationStatus.Draft;
        StartAt = startAt;
        EndAt = endAt;
        Purpose = purpose.Trim();
        PickupLocation = string.IsNullOrWhiteSpace(pickupLocation) ? null : pickupLocation.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid RequesterPersonId { get; private set; }
    public EquipmentReservationStatus Status { get; private set; }
    public DateTimeOffset StartAt { get; private set; }
    public DateTimeOffset EndAt { get; private set; }
    public string Purpose { get; private set; } = string.Empty;
    public string? PickupLocation { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset? RequestedAt { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public string? ApprovedBy { get; private set; }
    public DateTimeOffset? RejectedAt { get; private set; }
    public string? RejectedBy { get; private set; }
    public string? DecisionNotes { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public string? CancelledBy { get; private set; }
    public string? CancellationReason { get; private set; }
    public Guid? AssignmentId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public List<EquipmentReservationItem> Items { get; private set; } = [];

    public EquipmentReservationItem AddItem(Guid requestedCategoryId, int requestedQuantity, Guid? kitDefinitionId)
    {
        var item = new EquipmentReservationItem(OrganizationId, Id, requestedCategoryId, requestedQuantity, kitDefinitionId);
        Items.Add(item);
        UpdatedAt = DateTimeOffset.UtcNow;
        return item;
    }
}
