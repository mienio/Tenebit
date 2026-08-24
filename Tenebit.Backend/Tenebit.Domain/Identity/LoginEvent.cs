namespace Tenebit.Domain.Identity;

/// <summary>
/// Append-only record of an authentication attempt. Written for both successes and failures so the
/// admin panel can show a real sign-in history and surface password-guessing against an account.
/// Deliberately not tenant-filtered on write: a failed attempt for an unknown e-mail has no
/// organization to attribute it to, and those rows are exactly the ones worth seeing.
/// </summary>
public sealed class LoginEvent
{
    private LoginEvent() { }

    public LoginEvent(
        Guid? organizationId,
        Guid? userId,
        string email,
        bool succeeded,
        string? failureReason,
        string? ipAddress,
        string? userAgent,
        DateTimeOffset createdAt,
        DateTimeOffset? ipExpiresAt)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        UserId = userId;
        // Never store an unbounded attacker-controlled string; the column is capped at 320 (RFC max).
        Email = Truncate(email, 320) ?? string.Empty;
        Succeeded = succeeded;
        FailureReason = Truncate(failureReason, 64);
        IpAddress = Truncate(ipAddress, 64);
        UserAgent = Truncate(userAgent, 400);
        CreatedAt = createdAt;
        IpExpiresAt = ipExpiresAt;
    }

    public Guid Id { get; private set; }

    /// <summary>Null when the attempt used an e-mail that matches no account.</summary>
    public Guid? OrganizationId { get; private set; }

    public Guid? UserId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public bool Succeeded { get; private set; }
    public string? FailureReason { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// When the IP must be cleared. An IP address is personal data under GDPR, so it carries its own
    /// retention deadline independent of the event row, mirroring ActivityLog.SourceIpExpiresAt.
    /// </summary>
    public DateTimeOffset? IpExpiresAt { get; private set; }

    public void ForgetIpAddress()
    {
        IpAddress = null;
        IpExpiresAt = null;
    }

    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value) ? null
            : value.Length <= maxLength ? value
            : value[..maxLength];
}
