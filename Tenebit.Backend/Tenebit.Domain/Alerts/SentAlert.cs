namespace Tenebit.Domain.Alerts;

public sealed class SentAlert
{
    public const int LastErrorMaxLength = 500;

    private SentAlert() { }

    public SentAlert(Guid organizationId, string alertKey, Guid entityId, string recipientEmail, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        AlertKey = alertKey;
        EntityId = entityId;
        RecipientEmail = recipientEmail.Trim().ToLowerInvariant();
        Status = SentAlertStatus.Pending;
        AttemptCount = 0;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string AlertKey { get; private set; } = string.Empty;
    public Guid EntityId { get; private set; }
    public string RecipientEmail { get; private set; } = string.Empty;
    public SentAlertStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public DateTimeOffset? NextAttemptAt { get; private set; }
    public string? LastError { get; private set; }
    public Guid? DigestId { get; private set; }

    public bool CanRetry(int maxAttempts, DateTimeOffset now) =>
        Status == SentAlertStatus.Failed && AttemptCount < maxAttempts && (NextAttemptAt is null || NextAttemptAt <= now);

    public void MarkSent(DateTimeOffset sentAt)
    {
        AttemptCount++;
        Status = SentAlertStatus.Sent;
        SentAt = sentAt;
        NextAttemptAt = null;
        LastError = null;
    }

    public void MarkFailed(DateTimeOffset attemptedAt, string error, TimeSpan retryDelay)
    {
        AttemptCount++;
        Status = SentAlertStatus.Failed;
        LastError = error.Length > LastErrorMaxLength ? error[..LastErrorMaxLength] : error;
        NextAttemptAt = attemptedAt + retryDelay;
    }
}
