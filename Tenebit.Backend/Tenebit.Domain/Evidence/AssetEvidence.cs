using Tenebit.Domain.Common;

namespace Tenebit.Domain.Evidence;

public sealed class AssetEvidence
{
    private AssetEvidence() { }

    public AssetEvidence(Guid organizationId, Guid assetId, Guid? assignmentId, EvidencePhase phase,
        string fileName, string contentType, byte[] content, string sha256, string? caption,
        string uploadedBy, EvidenceUploadSource uploadedVia, DateTimeOffset uploadedAt)
    {
        if (content.Length == 0) throw new DomainException("Zdjęcie jest puste.");
        if (content.Length > 5 * 1024 * 1024) throw new DomainException("Zdjęcie może mieć maksymalnie 5 MB.");
        if (string.IsNullOrWhiteSpace(fileName)) throw new DomainException("Nazwa pliku jest wymagana.");
        if (string.IsNullOrWhiteSpace(sha256)) throw new DomainException("Suma kontrolna pliku jest wymagana.");

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        AssetId = assetId;
        AssignmentId = assignmentId;
        Phase = phase;
        FileName = Path.GetFileName(fileName.Trim());
        ContentType = contentType.Trim();
        Content = content;
        SizeBytes = content.LongLength;
        Sha256 = sha256.Trim().ToLowerInvariant();
        Caption = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim();
        UploadedBy = string.IsNullOrWhiteSpace(uploadedBy) ? "system" : uploadedBy.Trim();
        UploadedVia = uploadedVia;
        UploadedAt = uploadedAt;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid AssetId { get; private set; }
    public Guid? AssignmentId { get; private set; }
    public Guid? OffboardingItemId { get; private set; }
    public Guid? AssetAuditItemId { get; private set; }
    public EvidencePhase Phase { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public byte[] Content { get; private set; } = [];
    public long SizeBytes { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;
    public string? Caption { get; private set; }
    public DateTimeOffset UploadedAt { get; private set; }
    public string UploadedBy { get; private set; } = string.Empty;
    public EvidenceUploadSource UploadedVia { get; private set; }
    public DateTimeOffset? LockedAt { get; private set; }
    public bool LegalHold { get; private set; }
    public DateTimeOffset? RedactedAt { get; private set; }

    public void Lock(DateTimeOffset lockedAt)
    {
        if (LockedAt.HasValue) return;
        LockedAt = lockedAt;
    }

    public void EnsureDeletable()
    {
        if (LockedAt.HasValue) throw new DomainException("Zablokowany materiał dowodowy nie może zostać usunięty.");
    }

    public void SetLegalHold(bool enabled)
    {
        LegalHold = enabled;
    }

    /// <summary>Usuwa treść pliku po upływie retencji, zachowując rekord audytowy (nazwa, suma kontrolna, metadane).</summary>
    public bool Redact(DateTimeOffset redactedAt)
    {
        if (RedactedAt.HasValue) return false;
        if (LegalHold) return false;

        Content = [];
        SizeBytes = 0;
        RedactedAt = redactedAt;
        return true;
    }
}
