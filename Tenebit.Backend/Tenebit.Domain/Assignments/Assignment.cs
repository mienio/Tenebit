using Tenebit.Domain.Common;

namespace Tenebit.Domain.Assignments;

public sealed class Assignment
{
    private Assignment() { }

    public Assignment(Guid organizationId, Guid personId, string protocolNumber, DateTimeOffset issuedAt, DateOnly? dueDate, string? notes, string createdBy)
    {
        if (string.IsNullOrWhiteSpace(protocolNumber))
        {
            throw new DomainException("Numer protokołu jest wymagany.");
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        PersonId = personId;
        ProtocolNumber = protocolNumber.Trim();
        IssuedAt = issuedAt;
        DueDate = dueDate;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        CreatedBy = string.IsNullOrWhiteSpace(createdBy) ? "system" : createdBy.Trim();
        Status = AssignmentStatus.AwaitingAcceptance;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid PersonId { get; private set; }
    public AssignmentStatus Status { get; private set; }
    public DateTimeOffset IssuedAt { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }
    public DateTimeOffset? ReturnedAt { get; private set; }
    public string? Notes { get; private set; }
    public string ProtocolNumber { get; private set; } = string.Empty;
    public string CreatedBy { get; private set; } = string.Empty;
    public List<AssignmentAsset> Assets { get; private set; } = [];
    public List<ProcedureAcceptance> ProcedureAcceptances { get; private set; } = [];

    public void AddAsset(Guid assetId, string? issueCondition)
    {
        if (Assets.Any(x => x.AssetId == assetId))
        {
            throw new DomainException("Ten asset jest już dodany do wydania.");
        }

        Assets.Add(new AssignmentAsset(Id, assetId, issueCondition));
    }

    public void AddProcedureAcceptance(Guid organizationId, Guid procedureId, Guid personId, DateTimeOffset sentAt)
    {
        if (ProcedureAcceptances.Any(x => x.ProcedureId == procedureId))
        {
            return;
        }

        ProcedureAcceptances.Add(new ProcedureAcceptance(organizationId, procedureId, personId, Id, sentAt));
    }

    public void Accept(DateTimeOffset acceptedAt)
    {
        // BUG FIX: Previously allowed accepting already-accepted or returned/cancelled assignments.
        // Now only AwaitingAcceptance and Overdue statuses can transition to Accepted.
        if (Status is not AssignmentStatus.AwaitingAcceptance and not AssignmentStatus.Overdue)
        {
            throw new DomainException("Wydanie można zaakceptować tylko wtedy, gdy oczekuje na akceptację albo jest po terminie.");
        }

        Status = AssignmentStatus.Accepted;
        AcceptedAt = acceptedAt;
        foreach (var acceptance in ProcedureAcceptances)
        {
            acceptance.Accept(acceptedAt);
        }
    }

    public void Return(DateTimeOffset returnedAt, string? returnCondition, IReadOnlyDictionary<Guid, string?>? assetConditions = null)
    {
        // BUG FIX: Previously only blocked Returned status. Now also blocks Cancelled assignments.
        // A cancelled assignment cannot be returned — it was cancelled before completion.
        if (Status is AssignmentStatus.Returned or AssignmentStatus.Cancelled)
        {
            throw new DomainException("Nie można zwrócić wydania, które zostało już zamknięte lub anulowane.");
        }

        Status = AssignmentStatus.Returned;
        ReturnedAt = returnedAt;
        foreach (var item in Assets)
        {
            var condition = assetConditions is not null && assetConditions.TryGetValue(item.AssetId, out var perAsset) && !string.IsNullOrWhiteSpace(perAsset)
                ? perAsset
                : returnCondition;
            item.SetReturnCondition(condition);
        }
    }
}
