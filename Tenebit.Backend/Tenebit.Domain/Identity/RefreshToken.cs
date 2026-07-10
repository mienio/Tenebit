namespace Tenebit.Domain.Identity;

public sealed class RefreshToken
{
    private RefreshToken() { }

    public RefreshToken(Guid organizationUserId, string tokenHash, DateTimeOffset expiresAt)
    {
        Id = Guid.NewGuid();
        OrganizationUserId = organizationUserId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationUserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public bool IsValid(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    public void Revoke() => RevokedAt = DateTimeOffset.UtcNow;
}
