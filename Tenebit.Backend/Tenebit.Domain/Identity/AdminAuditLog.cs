namespace Tenebit.Domain.Identity;

/// <summary>
/// Append-only record of everything the platform admin does. Separate from ActivityLog because that one
/// is tenant-scoped (OrganizationId is required and query-filtered) while admin actions are
/// cross-tenant and must stay readable even for an organization that was suspended.
///
/// There is deliberately no delete/update path anywhere in the application: if the admin account is
/// ever taken over, this table is the record of what the attacker did, so the panel must not be able
/// to rewrite it.
/// </summary>
public sealed class AdminAuditLog
{
    private AdminAuditLog() { }

    public AdminAuditLog(
        string action,
        string? targetType,
        Guid? targetId,
        string? targetLabel,
        string? details,
        string? ipAddress,
        DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        Action = action;
        TargetType = targetType;
        TargetId = targetId;
        TargetLabel = Truncate(targetLabel, 240);
        Details = Truncate(details, 1000);
        IpAddress = Truncate(ipAddress, 64);
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    /// <summary>Stable machine-readable verb, e.g. "organization.suspended".</summary>
    public string Action { get; private set; } = string.Empty;

    public string? TargetType { get; private set; }
    public Guid? TargetId { get; private set; }

    /// <summary>Human-readable name captured at action time, so the entry stays meaningful after a rename.</summary>
    public string? TargetLabel { get; private set; }

    public string? Details { get; private set; }
    public string? IpAddress { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value) ? null
            : value.Length <= maxLength ? value
            : value[..maxLength];
}
