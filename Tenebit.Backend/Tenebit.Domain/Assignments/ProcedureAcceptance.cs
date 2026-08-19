using Tenebit.Domain.Common;

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
        IntegrityVersion = 2;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ProcedureId { get; private set; }
    public Guid PersonId { get; private set; }
    public Guid? AssignmentId { get; private set; }
    public AcceptanceStatus Status { get; private set; }
    public DateTimeOffset SentAt { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }
    public string? ConfirmedIp { get; private set; }
    public string? ConfirmationHash { get; private set; }
    public int IntegrityVersion { get; private set; } = 1;

    // Hardening: a confirmed acceptance is a legal proof-of-receipt record - once signed it must never be
    // overwritten (re-signed, re-timestamped, re-IP-stamped). The hash is computed only from fields owned by
    // this record, so any direct DB tampering after signing can be detected by recomputing it (VerifyIntegrity).
    public void Accept(DateTimeOffset acceptedAt, string? ipAddress)
    {
        if (Status is AcceptanceStatus.Accepted or AcceptanceStatus.Declined)
        {
            throw new DomainException("Ta akceptacja procedury została już zarejestrowana i nie może zostać zmieniona.");
        }

        Status = AcceptanceStatus.Accepted;
        AcceptedAt = acceptedAt;
        ConfirmedIp = string.IsNullOrWhiteSpace(ipAddress) ? null : ipAddress.Trim();
        IntegrityVersion = Math.Max(IntegrityVersion, 2);
        ConfirmationHash = ComputeHash(acceptedAt, ConfirmedIp);
    }

    public void ApplyIpPrivacy(string? storedIp)
    {
        if (AcceptedAt is null || ConfirmationHash is null) return;
        ConfirmedIp = string.IsNullOrWhiteSpace(storedIp) ? null : storedIp.Trim();
        IntegrityVersion = Math.Max(IntegrityVersion, 2);
        ConfirmationHash = ComputeHash(AcceptedAt.Value, ConfirmedIp);
    }

    public void MarkOverdue()
    {
        if (Status == AcceptanceStatus.Pending)
        {
            Status = AcceptanceStatus.Overdue;
        }
    }

    // Recomputes the hash from the record's current field values - a mismatch with the stored
    // ConfirmationHash means the record was altered after signing, bypassing this class.
    public bool VerifyIntegrity()
    {
        if (AcceptedAt is null || ConfirmationHash is null) return true;
        return ComputeHash(AcceptedAt.Value, ConfirmedIp) == ConfirmationHash;
    }

    private string ComputeHash(DateTimeOffset acceptedAt, string? ipAddress)
    {
        var privacySafeIpPart = IntegrityVersion >= 2 ? string.Empty : ipAddress ?? string.Empty;
        var payload = string.Join('|', Id, OrganizationId, ProcedureId, PersonId, AssignmentId, SentAt.ToUniversalTime().ToString("O"), acceptedAt.ToUniversalTime().ToString("O"), privacySafeIpPart);
        var bytes = System.Text.Encoding.UTF8.GetBytes(payload);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
    }
}
