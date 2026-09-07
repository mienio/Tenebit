using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Configuration;
using Tenebit.Api.Auth;
using Tenebit.Application.Identity;

namespace Tenebit.Tests;

/// <summary>
/// Unit-level guard for the identity isolation spec §4.1/§12.1 requires (a full "does the ASP.NET auth
/// pipeline actually reject a cross-scope token on the wrong route group" check needs a running app -
/// see Tenebit.Tests/Integration - and is intentionally deferred to that later pass). What IS unit
/// testable here: the affiliate scope claim is distinct from the platform-admin one, and an affiliate
/// token carries neither an organization_id claim (which the tenant OnTokenValidated branch would
/// otherwise misinterpret) nor the PlatformAdmin scope claim.
/// </summary>
public sealed class AffiliateScopeIsolationTests
{
    private static IConfiguration BuildConfiguration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Auth:Issuer"] = "issuer",
        ["Auth:Audience"] = "audience",
        ["Auth:ActiveSigningKeyId"] = "current",
        ["Auth:SigningKeys:current"] = "current-signing-key-which-is-long-enough-1234567"
    }).Build();

    [Fact]
    public void Affiliate_and_platform_admin_scope_values_are_distinct()
    {
        Assert.Equal("token_scope", AffiliateClaims.ScopeClaimType);
        Assert.Equal("token_scope", PlatformAdminClaims.ScopeClaimType);
        Assert.NotEqual(AffiliateClaims.ScopeValue, PlatformAdminClaims.ScopeValue);
    }

    [Fact]
    public void An_affiliate_token_carries_the_affiliate_scope_but_no_organization_id_or_admin_scope()
    {
        var issuer = new TokenIssuer(BuildConfiguration());
        var affiliateId = Guid.NewGuid();
        var token = new JwtSecurityTokenHandler().ReadJwtToken(issuer.IssueAffiliate(affiliateId, "damian@example.com", Guid.NewGuid(), 15));

        Assert.Equal(AffiliateClaims.ScopeValue, token.Claims.Single(c => c.Type == AffiliateClaims.ScopeClaimType).Value);
        Assert.Equal(affiliateId.ToString(), token.Claims.Single(c => c.Type == "sub").Value);
        Assert.DoesNotContain(token.Claims, c => c.Type == "organization_id");
        Assert.DoesNotContain(token.Claims, c => c.Type == PlatformAdminClaims.ScopeClaimType && c.Value == PlatformAdminClaims.ScopeValue);
    }

    [Fact]
    public void A_tenant_token_carries_no_affiliate_scope_claim()
    {
        var issuer = new TokenIssuer(BuildConfiguration());
        var user = new AuthUserResponse(Guid.NewGuid(), Guid.NewGuid(), "Org", "user@example.com", "User", ["Owner"], true, false, Guid.NewGuid());
        var token = new JwtSecurityTokenHandler().ReadJwtToken(issuer.Issue(user));

        // Both scopes reuse the same "token_scope" claim TYPE by design (one authorization dimension) -
        // isolation lives in the VALUE never matching, which is what actually matters here.
        Assert.DoesNotContain(token.Claims, c => c.Type == AffiliateClaims.ScopeClaimType && c.Value == AffiliateClaims.ScopeValue);
    }

    [Fact]
    public void A_platform_admin_token_carries_no_affiliate_scope_value()
    {
        var issuer = new TokenIssuer(BuildConfiguration());
        var token = new JwtSecurityTokenHandler().ReadJwtToken(issuer.IssueAdmin("admin@example.com", 15));

        Assert.DoesNotContain(token.Claims, c => c.Type == AffiliateClaims.ScopeClaimType && c.Value == AffiliateClaims.ScopeValue);
        Assert.Equal(PlatformAdminClaims.ScopeValue, token.Claims.Single(c => c.Type == PlatformAdminClaims.ScopeClaimType).Value);
    }
}
