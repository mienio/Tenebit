namespace Tenebit.Domain.Audit;

public sealed class ActivityLog
{
    private ActivityLog() { }

    public ActivityLog(Guid organizationId, string action, string entityType, Guid? entityId, string actorSubject, string? details, DateTimeOffset createdAt, string? sourceIp = null, DateTimeOffset? sourceIpExpiresAt = null)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        ActorSubject = actorSubject;
        Details = details;
        CreatedAt = createdAt;
        SourceIp = sourceIp;
        SourceIpExpiresAt = sourceIpExpiresAt;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public Guid? EntityId { get; private set; }
    public string ActorSubject { get; private set; } = string.Empty;
    public string? Details { get; private set; }
    public string? SourceIp { get; private set; }
    public DateTimeOffset? SourceIpExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
