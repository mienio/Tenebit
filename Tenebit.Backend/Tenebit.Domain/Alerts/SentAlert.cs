namespace Tenebit.Domain.Alerts;

public sealed class SentAlert
{
    private SentAlert() { }

    public SentAlert(Guid organizationId, string alertKey, Guid entityId, DateTimeOffset sentAt)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        AlertKey = alertKey;
        EntityId = entityId;
        SentAt = sentAt;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string AlertKey { get; private set; } = string.Empty;
    public Guid EntityId { get; private set; }
    public DateTimeOffset SentAt { get; private set; }
}
