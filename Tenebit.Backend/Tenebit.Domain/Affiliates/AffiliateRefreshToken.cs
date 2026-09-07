namespace Tenebit.Domain.Affiliates;

/// <summary>
/// Refresh token for an affiliate session - a completely separate table from
/// <see cref="Tenebit.Domain.Identity.RefreshToken"/> (tenant users), on purpose (spec §4.1/§12.1):
/// zero shared id space between the two session kinds, so a bug in one lookup can never be tricked
/// into accepting the other's token.
/// </summary>
public sealed class AffiliateRefreshToken
{
    private AffiliateRefreshToken() { }

    public AffiliateRefreshToken(Guid affiliateId, string tokenHash, DateTimeOffset expiresAt, Guid? familyId = null, Guid? parentTokenId = null)
    {
        Id = Guid.NewGuid();
        AffiliateId = affiliateId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        CreatedAt = DateTimeOffset.UtcNow;
        FamilyId = familyId ?? Id;
        ParentTokenId = parentTokenId;
    }

    public Guid Id { get; private set; }
    public Guid AffiliateId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid FamilyId { get; private set; }
    public Guid? ParentTokenId { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }
    public string? RevocationReason { get; private set; }

    public bool IsValid(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    public void Revoke(DateTimeOffset? at = null, string? reason = null)
    {
        RevokedAt ??= at ?? DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(reason)) RevocationReason = reason;
    }

    public void MarkRotated(Guid replacementTokenId, DateTimeOffset at)
    {
        ReplacedByTokenId = replacementTokenId;
        Revoke(at, "rotated");
    }
}
