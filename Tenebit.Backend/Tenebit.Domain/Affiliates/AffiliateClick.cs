namespace Tenebit.Domain.Affiliates;

/// <summary>
/// Raw click log for <c>GET /r/{code}</c> - technical tracking data, not a financial record. IP/User
/// Agent are stored hashed (HMAC, server-side pepper), never in plaintext: enough to dedupe/rate-limit
/// without holding personal data with no clear purpose (spec §12.11 RODO minimisation). Rows older
/// than 13 months (longest attribution window plus margin) are pruned by a retention job - unlike
/// AffiliateConversion this is never needed for a payout dispute.
/// </summary>
public sealed class AffiliateClick
{
    private AffiliateClick() { }

    public AffiliateClick(Guid affiliateCodeId, string ipHash, string? userAgentHash, Guid attributionToken, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        AffiliateCodeId = affiliateCodeId;
        IpHash = ipHash;
        UserAgentHash = userAgentHash;
        AttributionToken = attributionToken;
        ClickedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid AffiliateCodeId { get; private set; }
    public DateTimeOffset ClickedAt { get; private set; }
    public string IpHash { get; private set; } = string.Empty;
    public string? UserAgentHash { get; private set; }
    public Guid AttributionToken { get; private set; }
}
