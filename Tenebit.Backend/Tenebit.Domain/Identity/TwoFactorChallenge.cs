namespace Tenebit.Domain.Identity;

public sealed class TwoFactorChallenge
{
    private TwoFactorChallenge() { }

    public TwoFactorChallenge(string ticketHash, Guid organizationUserId, DateTimeOffset expiresAt)
    {
        Id = Guid.NewGuid();
        TicketHash = ticketHash;
        OrganizationUserId = organizationUserId;
        ExpiresAt = expiresAt;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string TicketHash { get; private set; } = string.Empty;
    public Guid OrganizationUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
}
