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
    public string? AcceptedIp { get; private set; }
    public string? AcceptanceHash { get; private set; }
    public List<AssignmentAsset> Assets { get; private set; } = [];
    public List<ProcedureAcceptance> ProcedureAcceptances { get; private set; } = [];

    public void AddAsset(Guid assetId, string? issueCondition)
    {
        EnsureNotSigned();
        if (Assets.Any(x => x.AssetId == assetId))
        {
            throw new DomainException("Ten asset jest już dodany do wydania.");
        }

        Assets.Add(new AssignmentAsset(Id, assetId, issueCondition));
    }

    public void AddProcedureAcceptance(Guid organizationId, Guid procedureId, Guid personId, DateTimeOffset sentAt)
    {
        EnsureNotSigned();
        if (ProcedureAcceptances.Any(x => x.ProcedureId == procedureId))
        {
            return;
        }

        ProcedureAcceptances.Add(new ProcedureAcceptance(organizationId, procedureId, personId, Id, sentAt));
    }

    // Hardening: the accepted protocol is a legal proof-of-receipt record — capture who signed it, when, from
    // where, and a hash of exactly what was confirmed (assets + conditions + procedures), so any later direct
    // edit to those rows can be detected by recomputing the hash (see VerifyIntegrity).
    public void Accept(DateTimeOffset acceptedAt, string? ipAddress)
    {
        // BUG FIX: Previously allowed accepting already-accepted or returned/cancelled assignments.
        // Now only AwaitingAcceptance and Overdue statuses can transition to Accepted.
        if (Status is not AssignmentStatus.AwaitingAcceptance and not AssignmentStatus.Overdue)
        {
            throw new DomainException("Wydanie można zaakceptować tylko wtedy, gdy oczekuje na akceptację albo jest po terminie.");
        }

        Status = AssignmentStatus.Accepted;
        AcceptedAt = acceptedAt;
        AcceptedIp = string.IsNullOrWhiteSpace(ipAddress) ? null : ipAddress.Trim();
        AcceptanceHash = ComputeHash(acceptedAt, AcceptedIp);
        foreach (var acceptance in ProcedureAcceptances)
        {
            acceptance.Accept(acceptedAt, ipAddress);
        }
    }

    // Recomputes the hash from the assignment's current field values — a mismatch with the stored
    // AcceptanceHash means the protocol was altered after signing, bypassing this class.
    public bool VerifyIntegrity()
    {
        if (AcceptedAt is null || AcceptanceHash is null) return true;
        return ComputeHash(AcceptedAt.Value, AcceptedIp) == AcceptanceHash;
    }

    public void MarkOverdue()
    {
        if (Status == AssignmentStatus.AwaitingAcceptance)
        {
            Status = AssignmentStatus.Overdue;
        }
    }

    private void EnsureNotSigned()
    {
        if (Status is AssignmentStatus.Accepted or AssignmentStatus.Returned or AssignmentStatus.Cancelled)
        {
            throw new DomainException("Nie można modyfikować wydania, które zostało już podpisane lub zamknięte.");
        }
    }

    private string ComputeHash(DateTimeOffset acceptedAt, string? ipAddress)
    {
        var assetsPart = string.Join(',', Assets.OrderBy(x => x.AssetId).Select(x => $"{x.AssetId}:{x.IssueCondition}"));
        var proceduresPart = string.Join(',', ProcedureAcceptances.Select(x => x.ProcedureId).OrderBy(x => x));
        var payload = string.Join('|', Id, OrganizationId, PersonId, ProtocolNumber, assetsPart, proceduresPart, acceptedAt.ToUniversalTime().ToString("O"), ipAddress ?? "");
        var bytes = System.Text.Encoding.UTF8.GetBytes(payload);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
    }

    public void Return(DateTimeOffset returnedAt, string? returnCondition)
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
            item.SetReturnCondition(returnCondition);
        }
    }
}
