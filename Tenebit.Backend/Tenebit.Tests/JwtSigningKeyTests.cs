using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Configuration;
using Tenebit.Api.Auth;
using Tenebit.Application.Identity;

namespace Tenebit.Tests;

public sealed class JwtSigningKeyTests
{
    [Fact]
    public void TokenIssuer_UsesActiveSigningKeyId()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Auth:Issuer"] = "issuer",
            ["Auth:Audience"] = "audience",
            ["Auth:ActiveSigningKeyId"] = "current",
            ["Auth:SigningKeys:previous"] = "previous-signing-key-which-is-long-enough-123456",
            ["Auth:SigningKeys:current"] = "current-signing-key-which-is-long-enough-1234567"
        });
        var issuer = new TokenIssuer(configuration);
        var user = new AuthUserResponse(
            Guid.NewGuid(), Guid.NewGuid(), "Org", "user@example.com", "User", ["Owner"], true, false,
            Guid.NewGuid());

        var token = new JwtSecurityTokenHandler().ReadJwtToken(issuer.Issue(user));

        Assert.Equal("current", token.Header.Kid);
    }

    [Fact]
    public void ValidationKeys_ResolveByKid_AndSupportLegacyTokensWithoutKid()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Auth:ActiveSigningKeyId"] = "current",
            ["Auth:SigningKeys:previous"] = "previous-signing-key-which-is-long-enough-123456",
            ["Auth:SigningKeys:current"] = "current-signing-key-which-is-long-enough-1234567"
        });

        Assert.Single(JwtSigningKey.GetValidationKeys(configuration, "current"));
        Assert.Empty(JwtSigningKey.GetValidationKeys(configuration, "missing"));
        Assert.Equal(2, JwtSigningKey.GetValidationKeys(configuration, null).Count);
    }


    [Fact]
    public void ExplicitKeyRing_IgnoresLegacyAppSettingsSigningKey()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Auth:SigningKey"] = "tenebit-development-signing-key-change-me-32chars",
            ["Auth:SigningKeyId"] = "legacy",
            ["Auth:ActiveSigningKeyId"] = "current",
            ["Auth:SigningKeys:previous"] = "previous-signing-key-which-is-long-enough-123456",
            ["Auth:SigningKeys:current"] = "current-signing-key-which-is-long-enough-1234567"
        });

        var configured = JwtSigningKey.GetConfiguredSecrets(configuration);

        Assert.Equal(2, configured.Count);
        Assert.DoesNotContain("legacy", configured.Keys);
        Assert.Equal("current", JwtSigningKey.GetActive(configuration).KeyId);
    }

    private static IConfiguration BuildConfiguration(IReadOnlyDictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
