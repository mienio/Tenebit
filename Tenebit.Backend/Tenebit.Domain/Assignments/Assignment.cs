using Tenebit.Domain.Common;
using Tenebit.Domain.Evidence;

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
    public int IntegrityVersion { get; private set; } = 1;
    public string? PublicTokenHash { get; private set; }
    public DateTimeOffset? PublicTokenExpiresAt { get; private set; }
    public DateTimeOffset? PublicTokenRevokedAt { get; private set; }
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
    public void Accept(DateTimeOffset acceptedAt, string? ipAddress, IReadOnlyList<AssetEvidence>? evidence = null)
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
        AcceptanceHash = ComputeHash(acceptedAt, AcceptedIp, evidence);
        foreach (var acceptance in ProcedureAcceptances)
        {
            acceptance.Accept(acceptedAt, ipAddress);
        }
    }

    // Recomputes the hash from the assignment's current field values — a mismatch with the stored
    // AcceptanceHash means the protocol was altered after signing, bypassing this class.
    public bool VerifyIntegrity(IReadOnlyList<AssetEvidence>? evidence = null)
    {
        if (AcceptedAt is null || AcceptanceHash is null) return true;
        return ComputeHash(AcceptedAt.Value, AcceptedIp, evidence) == AcceptanceHash;
    }

    // Spec 6.6: wersja 2 obejmuje zdjęcia wydania w hashu akceptacji. Wersja 1 pozostaje bez zmian
    // dla istniejących protokołów — nie przeliczamy ich nowym algorytmem.
    public void EnableEvidenceIntegrity()
    {
        IntegrityVersion = 2;
    }

    // AUD-001: publiczny link akceptacji identyfikuje wydanie tokenem (hash w DB, TTL, revoke),
    // nie samym OrganizationId+Id — patrz PublicTokenService dla generacji/weryfikacji.
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

    public void MarkOverdue()
    {
        if (Status == AssignmentStatus.AwaitingAcceptance)
        {
            Status = AssignmentStatus.Overdue;
        }
    }

    private void EnsureNotSigned()
    {
        if (Status is AssignmentStatus.Accepted or AssignmentStatus.Returned or AssignmentStatus.Cancelled or AssignmentStatus.PartiallyReturned)
        {
            throw new DomainException("Nie można modyfikować wydania, które zostało już podpisane lub zamknięte.");
        }
    }

    private string ComputeHash(DateTimeOffset acceptedAt, string? ipAddress, IReadOnlyList<AssetEvidence>? evidence)
    {
        var assetsPart = string.Join(',', Assets.OrderBy(x => x.AssetId).Select(x => $"{x.AssetId}:{x.IssueCondition}"));
        var proceduresPart = string.Join(',', ProcedureAcceptances.Select(x => x.ProcedureId).OrderBy(x => x));
        var payload = string.Join('|', Id, OrganizationId, PersonId, ProtocolNumber, assetsPart, proceduresPart, acceptedAt.ToUniversalTime().ToString("O"), ipAddress ?? "");

        if (IntegrityVersion >= 2)
        {
            // Zdjęcia wydania są częścią potwierdzonego protokołu (spec 6.6). Zdjęcia zwrotu dodawane później
            // nie mogą zmieniać hashu akceptacji, dlatego bierzemy pod uwagę wyłącznie fazę Issue.
            var entries = evidence ?? Array.Empty<AssetEvidence>();
            var evidencePart = string.Join(',', entries
                .Where(x => x.Phase == EvidencePhase.Issue)
                .OrderBy(x => x.Id)
                .Select(x => $"{x.Id}:{x.Phase}:{x.Sha256}"));
            payload = string.Join('|', payload, evidencePart);
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(payload);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
    }

    public void ReturnAsset(Guid assetId, ReturnResolution resolution, DateTimeOffset returnedAt, string? returnCondition, string? returnLocation, string? returnedBy, string? notes)
    {
        if (Status == AssignmentStatus.Cancelled)
        {
            throw new DomainException("Nie można zwrócić aktywa dla anulowanego wydania.");
        }

        var item = Assets.FirstOrDefault(x => x.AssetId == assetId);
        if (item is null)
        {
            throw new DomainException("To aktywo nie należy do tego wydania.");
        }

        if (item.ReturnResolution is not null)
        {
            return;
        }

        item.Resolve(resolution, returnedAt, returnCondition, returnLocation, returnedBy, notes);

        if (Assets.All(x => x.ReturnResolution is not null))
        {
            Status = AssignmentStatus.Returned;
            ReturnedAt = returnedAt;
        }
        else
        {
            Status = AssignmentStatus.PartiallyReturned;
        }
    }
}
