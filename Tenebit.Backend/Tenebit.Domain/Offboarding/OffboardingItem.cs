using Tenebit.Domain.Common;

namespace Tenebit.Domain.Offboarding;

public static class OffboardingItemStatuses
{
    public static readonly OffboardingItemStatus[] Terminal =
    [
        OffboardingItemStatus.Returned,
        OffboardingItemStatus.Released,
        OffboardingItemStatus.Missing,
        OffboardingItemStatus.Damaged,
        OffboardingItemStatus.Retained,
        OffboardingItemStatus.Waived
    ];
}

public sealed class OffboardingItem
{
    private OffboardingItem() { }

    public OffboardingItem(Guid organizationId, Guid offboardingCaseId, OffboardingItemType type, string label, bool required,
        Guid? assetId, Guid? assignmentId, Guid? licenseId, OffboardingItemAutomationMode automationMode, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            throw new DomainException("Nazwa pozycji jest wymagana.");
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        OffboardingCaseId = offboardingCaseId;
        Type = type;
        Label = label.Trim();
        Required = required;
        AssetId = assetId;
        AssignmentId = assignmentId;
        LicenseId = licenseId;
        AutomationMode = automationMode;
        SortOrder = sortOrder;
        Status = OffboardingItemStatus.Pending;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid OffboardingCaseId { get; private set; }
    public OffboardingItemType Type { get; private set; }
    public Guid? AssetId { get; private set; }
    public Guid? AssignmentId { get; private set; }
    public Guid? LicenseId { get; private set; }
    public string Label { get; private set; } = string.Empty;
    public bool Required { get; private set; }
    public OffboardingItemStatus Status { get; private set; }
    public string? EmployeeResponse { get; private set; }
    public string? EmployeeComment { get; private set; }
    public OffboardingItemAutomationMode AutomationMode { get; private set; }
    public DateTimeOffset? AutomationLastAttemptAt { get; private set; }
    public string? AutomationError { get; private set; }
    public DateTimeOffset? ReceivedAt { get; private set; }
    public string? ReceivedBy { get; private set; }
    public DateTimeOffset? InspectionCompletedAt { get; private set; }
    public string? InspectionCompletedBy { get; private set; }
    public string? ResolutionNotes { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? CompletedBy { get; private set; }
    public int SortOrder { get; private set; }

    public bool IsResolved => OffboardingItemStatuses.Terminal.Contains(Status);

    public void RecordEmployeeResponse(string response, string? comment)
    {
        if (IsResolved)
        {
            throw new DomainException("Nie można zmienić odpowiedzi pracownika po rozliczeniu pozycji.");
        }

        EmployeeResponse = string.IsNullOrWhiteSpace(response) ? null : response.Trim();
        EmployeeComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (Status == OffboardingItemStatus.Pending)
        {
            Status = OffboardingItemStatus.EmployeeAcknowledged;
        }
    }

    public void MarkReceived(DateTimeOffset receivedAt, string receivedBy)
    {
        if (IsResolved)
        {
            throw new DomainException("Pozycja jest już rozliczona.");
        }

        Status = OffboardingItemStatus.Received;
        ReceivedAt = receivedAt;
        ReceivedBy = string.IsNullOrWhiteSpace(receivedBy) ? "system" : receivedBy.Trim();
    }

    /// <summary>Kończy kontrolę odebranego sprzętu wynikiem pozytywnym (Returned). Wyniki negatywne obsługuje Resolve.</summary>
    public void CompleteInspection(DateTimeOffset completedAt, string completedBy)
    {
        if (IsResolved)
        {
            throw new DomainException("Pozycja jest już rozliczona.");
        }

        InspectionCompletedAt = completedAt;
        InspectionCompletedBy = string.IsNullOrWhiteSpace(completedBy) ? "system" : completedBy.Trim();
        Complete(OffboardingItemStatus.Returned, completedAt, completedBy);
    }

    public void Resolve(OffboardingItemStatus status, string notes, string actor, DateTimeOffset at)
    {
        if (status is not (OffboardingItemStatus.Missing or OffboardingItemStatus.Damaged or OffboardingItemStatus.Retained))
        {
            throw new DomainException("Ten status wymaga jawnego rozstrzygnięcia (Missing, Damaged albo Retained).");
        }

        if (string.IsNullOrWhiteSpace(notes))
        {
            throw new DomainException("Uzasadnienie jest wymagane.");
        }

        if (string.IsNullOrWhiteSpace(actor))
        {
            throw new DomainException("Osoba rozstrzygająca jest wymagana.");
        }

        if (IsResolved)
        {
            throw new DomainException("Pozycja jest już rozliczona.");
        }

        ResolutionNotes = notes.Trim();
        Complete(status, at, actor);
    }

    public void Waive(string reason, string actor, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("Powód odstąpienia jest wymagany.");
        }

        if (string.IsNullOrWhiteSpace(actor))
        {
            throw new DomainException("Osoba odstępująca jest wymagana.");
        }

        if (IsResolved)
        {
            throw new DomainException("Pozycja jest już rozliczona.");
        }

        ResolutionNotes = reason.Trim();
        Complete(OffboardingItemStatus.Waived, at, actor);
    }

    /// <summary>Zwalnia miejsce licencyjne - jedyna droga do stanu końcowego Released.</summary>
    public void MarkReleased(DateTimeOffset releasedAt, string releasedBy)
    {
        if (IsResolved)
        {
            throw new DomainException("Pozycja jest już rozliczona.");
        }

        Complete(OffboardingItemStatus.Released, releasedAt, releasedBy);
    }

    public void RecordAutomationFailure(DateTimeOffset attemptedAt, string error)
    {
        AutomationLastAttemptAt = attemptedAt;
        AutomationError = string.IsNullOrWhiteSpace(error) ? null : error.Trim();
    }

    private void Complete(OffboardingItemStatus status, DateTimeOffset at, string actor)
    {
        Status = status;
        CompletedAt = at;
        CompletedBy = string.IsNullOrWhiteSpace(actor) ? "system" : actor.Trim();
    }
}
