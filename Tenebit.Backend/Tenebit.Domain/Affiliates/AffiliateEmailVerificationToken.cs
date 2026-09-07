namespace Tenebit.Domain.Affiliates;

/// <summary>Separate table from <see cref="Tenebit.Domain.Identity.EmailVerificationToken"/> for the
/// same isolation reason as <see cref="AffiliateRefreshToken"/>.</summary>
public sealed class AffiliateEmailVerificationToken
{
    private AffiliateEmailVerificationToken() { }

    public AffiliateEmailVerificationToken(Guid affiliateId, string tokenHash, DateTimeOffset expiresAt)
    {
        Id = Guid.NewGuid();
        AffiliateId = affiliateId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid AffiliateId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? UsedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public bool IsValid(DateTimeOffset now) => UsedAt is null && ExpiresAt > now;

    public void MarkUsed() => UsedAt = DateTimeOffset.UtcNow;
}
