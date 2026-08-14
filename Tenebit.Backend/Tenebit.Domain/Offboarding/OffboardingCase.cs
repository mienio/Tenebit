using Tenebit.Domain.Common;

namespace Tenebit.Domain.Offboarding;

public sealed class OffboardingCase
{
    private OffboardingCase() { }

    public OffboardingCase(Guid organizationId, Guid personId, DateTimeOffset employmentEndsAt, DateTimeOffset returnDueDate,
        string? defaultReturnLocation, string? notes, Guid? processOwnerId,
        bool blockNewReservations, bool cancelFutureReservations, bool autoReleaseLicenses,
        string createdBy, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        PersonId = personId;
        EmploymentEndsAt = employmentEndsAt;
        ReturnDueDate = returnDueDate;
        DefaultReturnLocation = string.IsNullOrWhiteSpace(defaultReturnLocation) ? null : defaultReturnLocation.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        ProcessOwnerId = processOwnerId;
        BlockNewReservations = blockNewReservations;
        CancelFutureReservations = cancelFutureReservations;
        AutoReleaseLicenses = autoReleaseLicenses;
        CreatedBy = string.IsNullOrWhiteSpace(createdBy) ? "system" : createdBy.Trim();
        CreatedAt = createdAt;
        Status = OffboardingCaseStatus.Draft;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid PersonId { get; private set; }
    public OffboardingCaseStatus Status { get; private set; }
    public DateTimeOffset EmploymentEndsAt { get; private set; }
    public DateTimeOffset ReturnDueDate { get; private set; }
    public string? DefaultReturnLocation { get; private set; }
    public string? Notes { get; private set; }
    public Guid? ProcessOwnerId { get; private set; }
    public bool BlockNewReservations { get; private set; }
    public bool CancelFutureReservations { get; private set; }
    public bool AutoReleaseLicenses { get; private set; }
    public DateTimeOffset? PersonDeactivatedAt { get; private set; }
    public DateTimeOffset? ScheduledActionsCompletedAt { get; private set; }
    public string? PublicTokenHash { get; private set; }
    public DateTimeOffset? PublicTokenExpiresAt { get; private set; }
    public DateTimeOffset? PublicTokenRevokedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? CompletedBy { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }
    public string? FinalProtocolNumber { get; private set; }

    public void Start(DateTimeOffset startedAt)
    {
        if (Status != OffboardingCaseStatus.Draft)
        {
            throw new DomainException("Offboarding można uruchomić tylko ze statusu roboczego.");
        }

        Status = OffboardingCaseStatus.Active;
        StartedAt ??= startedAt;
    }

    /// <summary>Wylicza status na podstawie rozliczenia pozycji. ReadyToClose jest tu jedynie kandydatem
    /// operacyjnym — realne zamknięcie (<see cref="Complete"/>) wymaga dodatkowo dezaktywacji osoby.</summary>
    public void RecomputeStatus(IReadOnlyCollection<OffboardingItem> items, DateTimeOffset now)
    {
        if (Status is OffboardingCaseStatus.Draft or OffboardingCaseStatus.Completed or OffboardingCaseStatus.Cancelled)
        {
            return;
        }

        var requiredItems = items.Where(i => i.Required).ToList();
        var allRequiredResolved = requiredItems.Count == 0 || requiredItems.All(i => i.IsResolved);

        if (allRequiredResolved)
        {
            Status = OffboardingCaseStatus.ReadyToClose;
            return;
        }

        Status = PersonDeactivatedAt.HasValue || now > ReturnDueDate
            ? OffboardingCaseStatus.WaitingForReturn
            : OffboardingCaseStatus.Active;
    }

    /// <summary>Idempotentne: ponowne wywołanie na już zakończonej sprawie nic nie zmienia i nie tworzy drugiego protokołu.</summary>
    public void Complete(DateTimeOffset completedAt, string completedBy, string protocolNumber)
    {
        if (Status == OffboardingCaseStatus.Completed)
        {
            return;
        }

        if (Status != OffboardingCaseStatus.ReadyToClose)
        {
            throw new DomainException("Nie można zamknąć sprawy z nierozliczonymi wymaganymi pozycjami.");
        }

        if (!PersonDeactivatedAt.HasValue)
        {
            throw new DomainException("Nie można zamknąć sprawy przed zaplanowaną dezaktywacją osoby.");
        }

        Status = OffboardingCaseStatus.Completed;
        CompletedAt = completedAt;
        CompletedBy = string.IsNullOrWhiteSpace(completedBy) ? "system" : completedBy.Trim();
        FinalProtocolNumber = string.IsNullOrWhiteSpace(protocolNumber) ? null : protocolNumber.Trim();
        PublicTokenRevokedAt ??= completedAt;
    }

    /// <summary>Dozwolone tylko przed dezaktywacją osoby — po niej korektę pomyłki obsługuje <see cref="RestoreEmployment"/>.</summary>
    public void Cancel(DateTimeOffset cancelledAt, string reason)
    {
        if (Status == OffboardingCaseStatus.Cancelled)
        {
            return;
        }

        if (Status == OffboardingCaseStatus.Completed)
        {
            throw new DomainException("Nie można anulować zakończonej sprawy.");
        }

        if (PersonDeactivatedAt.HasValue)
        {
            throw new DomainException("Nie można anulować offboardingu po dezaktywacji osoby.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("Powód anulowania jest wymagany.");
        }

        Status = OffboardingCaseStatus.Cancelled;
        CancelledAt = cancelledAt;
        CancellationReason = reason.Trim();
    }

    /// <summary>Korekta pomyłkowej dezaktywacji. Nie przydziela automatycznie zwolnionych licencji ani zwróconych
    /// aktywów — to wymaga ręcznej odbudowy w kolejnym kroku procesu.</summary>
    public void RestoreEmployment(DateTimeOffset restoredAt)
    {
        if (!PersonDeactivatedAt.HasValue)
        {
            throw new DomainException("Przywrócenie zatrudnienia dotyczy tylko spraw po dezaktywacji osoby.");
        }

        Status = OffboardingCaseStatus.Cancelled;
        CancelledAt = restoredAt;
        CancellationReason = "Przywrócenie zatrudnienia";
    }

    public void MarkPersonDeactivated(DateTimeOffset at)
    {
        PersonDeactivatedAt ??= at;
    }

    public void MarkScheduledActionsCompleted(DateTimeOffset at)
    {
        ScheduledActionsCompletedAt ??= at;
    }

    public void SetPublicToken(string tokenHash, DateTimeOffset expiresAt)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainException("Hash tokenu jest wymagany.");
        }

        PublicTokenHash = tokenHash.Trim();
        PublicTokenExpiresAt = expiresAt;
        PublicTokenRevokedAt = null;
    }

    public void RevokePublicToken(DateTimeOffset revokedAt)
    {
        PublicTokenRevokedAt ??= revokedAt;
    }
}
