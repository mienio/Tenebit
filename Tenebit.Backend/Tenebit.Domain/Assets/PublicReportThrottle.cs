namespace Tenebit.Domain.Assets;

/// <summary>
/// One accepted public issue report, kept only long enough to enforce the rate limits.
///
/// Deliberately separate from the activity log. The limit used to be derived from audit rows, whose
/// actor is the constant "public-scan", so every reporter shared one identity and a single report
/// silenced everybody else on that asset. Audit answers "what happened"; this answers "who has
/// knocked recently" - two different questions with two different retention needs.
///
/// <see cref="ReporterHash"/> is a slow one-way pseudonym of the reporter's address, never the
/// address itself: the table can enforce per-reporter limits without becoming a log of who scanned
/// what, and it stays useful even when the organization has IP capture switched off.
/// </summary>
public sealed class PublicReportThrottle
{
    private PublicReportThrottle() { }

    public PublicReportThrottle(Guid organizationId, Guid assetId, string reporterHash, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        AssetId = assetId;
        ReporterHash = reporterHash;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid AssetId { get; private set; }
    public string ReporterHash { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
}
