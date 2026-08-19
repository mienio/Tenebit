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

    public void Submit(DateTimeOffset requestedAt)
    {
        if (Status != EquipmentReservationStatus.Draft)
        {
            throw new DomainException("Wniosek można złożyć tylko ze statusu roboczego.");
        }

        if (EndAt <= StartAt)
        {
            throw new DomainException("Data zakończenia musi być późniejsza niż data rozpoczęcia.");
        }

        if (StartAt < requestedAt)
        {
            throw new DomainException("Data rozpoczęcia rezerwacji nie może być w przeszłości.");
        }

        if (Items.Count == 0)
        {
            throw new DomainException("Wniosek musi zawierać co najmniej jedną pozycję.");
        }

        Status = EquipmentReservationStatus.PendingApproval;
        RequestedAt = requestedAt;
        UpdatedAt = requestedAt;
    }

    public void Approve(DateTimeOffset approvedAt, string approvedBy)
    {
        if (Status != EquipmentReservationStatus.PendingApproval)
        {
            throw new DomainException("Zatwierdzić można tylko wniosek oczekujący na akceptację.");
        }

        Status = EquipmentReservationStatus.Approved;
        ApprovedAt = approvedAt;
        ApprovedBy = string.IsNullOrWhiteSpace(approvedBy) ? "system" : approvedBy.Trim();
        UpdatedAt = approvedAt;
    }

    public void Reject(DateTimeOffset rejectedAt, string rejectedBy, string reason)
    {
        if (Status == EquipmentReservationStatus.Rejected)
        {
            return;
        }

        if (Status != EquipmentReservationStatus.PendingApproval)
        {
            throw new DomainException("Odrzucić można tylko wniosek oczekujący na akceptację.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("Powód odrzucenia jest wymagany.");
        }

        Status = EquipmentReservationStatus.Rejected;
        RejectedAt = rejectedAt;
        RejectedBy = string.IsNullOrWhiteSpace(rejectedBy) ? "system" : rejectedBy.Trim();
        DecisionNotes = reason.Trim();
        UpdatedAt = rejectedAt;
    }

    public void Cancel(DateTimeOffset cancelledAt, string cancelledBy, string? reason)
    {
        if (Status == EquipmentReservationStatus.Cancelled)
        {
            return;
        }

        if (Status is not (EquipmentReservationStatus.Draft or EquipmentReservationStatus.PendingApproval
            or EquipmentReservationStatus.Approved or EquipmentReservationStatus.ReadyForPickup))
        {
            throw new DomainException("Anulować można tylko wniosek przed wydaniem sprzętu.");
        }

        Status = EquipmentReservationStatus.Cancelled;
        CancelledAt = cancelledAt;
        CancelledBy = string.IsNullOrWhiteSpace(cancelledBy) ? "system" : cancelledBy.Trim();
        CancellationReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        UpdatedAt = cancelledAt;
    }

    public void UpdateDraft(DateTimeOffset startAt, DateTimeOffset endAt, string purpose, string? pickupLocation, string? notes)
    {
        if (Status != EquipmentReservationStatus.Draft)
        {
            throw new DomainException("Wniosek można edytować tylko w statusie roboczym.");
        }

        if (string.IsNullOrWhiteSpace(purpose))
        {
            throw new DomainException("Cel rezerwacji jest wymagany.");
        }

        if (endAt <= startAt)
        {
            throw new DomainException("Data zakończenia musi być późniejsza niż data rozpoczęcia.");
        }

        StartAt = startAt;
        EndAt = endAt;
        Purpose = purpose.Trim();
        PickupLocation = string.IsNullOrWhiteSpace(pickupLocation) ? null : pickupLocation.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ReplaceItems(IEnumerable<(Guid CategoryId, int Quantity, Guid? KitDefinitionId)> items)
    {
        if (Status != EquipmentReservationStatus.Draft)
        {
            throw new DomainException("Pozycje wniosku można edytować tylko w statusie roboczym.");
        }

        Items.Clear();
        foreach (var (categoryId, quantity, kitDefinitionId) in items)
        {
            AddItem(categoryId, quantity, kitDefinitionId);
        }

        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Akcja "Wydaj sprzęt" (spec 8.8) - wymaga, żeby każda pozycja miała już przydzielone aktywo
    /// (przydział/zamiana dzieje się wcześniej, przy zatwierdzeniu). Backend re-weryfikuje dostępność aktywów
    /// przed wywołaniem tej metody (w serwisie), więc tutaj pilnujemy tylko spójności stanu.</summary>
    public void MarkCheckedOut(Guid assignmentId, DateTimeOffset at)
    {
        if (Status is not (EquipmentReservationStatus.Approved or EquipmentReservationStatus.ReadyForPickup))
        {
            throw new DomainException("Wydać sprzęt można tylko z zatwierdzonego wniosku gotowego do odbioru.");
        }

        if (Items.Any(x => x.AssetId is null))
        {
            throw new DomainException("Wszystkie pozycje muszą mieć przydzielone aktywo przed wydaniem.");
        }

        AssignmentId = assignmentId;
        Status = EquipmentReservationStatus.CheckedOut;
        UpdatedAt = at;
        foreach (var item in Items)
        {
            item.MarkCheckedOut();
        }
    }

    /// <summary>Domknięcie po pełnym zwrocie powiązanego wydania (spec 8.8/8.12) - wywoływane przez
    /// AssignmentService, gdy wszystkie pozycje tego wydania mają już ustaloną rezolucję zwrotu.</summary>
    public void Complete(DateTimeOffset at)
    {
        if (Status != EquipmentReservationStatus.CheckedOut)
        {
            throw new DomainException("Zakończyć można tylko wydaną rezerwację.");
        }

        Status = EquipmentReservationStatus.Completed;
        UpdatedAt = at;
        foreach (var item in Items.Where(x => x.Status == EquipmentReservationItemStatus.CheckedOut))
        {
            item.MarkReturned();
        }
    }
}
