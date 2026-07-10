namespace Tenebit.Domain.Assignments;

public sealed class ProcedureAcceptance
{
    private ProcedureAcceptance() { }

    public ProcedureAcceptance(Guid organizationId, Guid procedureId, Guid personId, Guid? assignmentId, DateTimeOffset sentAt)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        ProcedureId = procedureId;
        PersonId = personId;
        AssignmentId = assignmentId;
        SentAt = sentAt;
        Status = AcceptanceStatus.Pending;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ProcedureId { get; private set; }
    public Guid PersonId { get; private set; }
    public Guid? AssignmentId { get; private set; }
    public AcceptanceStatus Status { get; private set; }
    public DateTimeOffset SentAt { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }

    public void Accept(DateTimeOffset acceptedAt)
    {
        Status = AcceptanceStatus.Accepted;
        AcceptedAt = acceptedAt;
    }

    public void MarkOverdue()
    {
        if (Status == AcceptanceStatus.Pending)
        {
            Status = AcceptanceStatus.Overdue;
        }
    }
}
