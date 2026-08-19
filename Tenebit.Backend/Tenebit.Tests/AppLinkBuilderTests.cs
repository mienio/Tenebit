using Microsoft.Extensions.Configuration;
using Tenebit.Infrastructure.Services;

namespace Tenebit.Tests;

public class AppLinkBuilderTests
{
    private static AppLinkBuilder CreateBuilder()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["App:PublicUrl"] = "https://app.tenebit.test" })
            .Build();
        return new AppLinkBuilder(configuration);
    }

    [Fact]
    public void BuildAppUrl_JoinsRelativePathToConfiguredOrigin()
    {
        var builder = CreateBuilder();

        var url = builder.BuildAppUrl("/dashboard?checkout=success");

        Assert.Equal("https://app.tenebit.test/dashboard?checkout=success", url);
    }

    [Theory]
    [InlineData("https://evil.test/phish")]
    [InlineData("//evil.test/phish")]
    [InlineData("not-a-path")]
    public void BuildAppUrl_RejectsNonRelativePath_AndFallsBackToDashboard(string maliciousPath)
    {
        var builder = CreateBuilder();

        var url = builder.BuildAppUrl(maliciousPath);

        Assert.Equal("https://app.tenebit.test/dashboard", url);
    }
    [Fact]
    public void SecretCapabilityLinks_PlaceCredentialOnlyInUrlFragment()
    {
        var builder = CreateBuilder();
        const string secret = "RAW_TEST_SECRET";
        var links = new[]
        {
            builder.BuildAssignmentAcceptanceLink(secret),
            builder.BuildOffboardingLink(secret),
            builder.BuildAssetAuditLink(secret)
        };

        foreach (var link in links)
        {
            var uri = new Uri(link);
            Assert.DoesNotContain(secret, uri.GetLeftPart(UriPartial.Path));
            Assert.DoesNotContain(secret, uri.Query);
            Assert.Equal($"#{secret}", uri.Fragment);
        }
    }

    [Fact]
    public void RecoveryLinks_PlaceEmailAndCodeOnlyInUrlFragment()
    {
        var builder = CreateBuilder();
        const string email = "owner+test@tenebit.test";
        const string code = "123456";
        var links = new[]
        {
            builder.BuildPasswordResetLink(email, code),
            builder.BuildEmailVerificationLink(email, code)
        };

        foreach (var link in links)
        {
            var uri = new Uri(link);
            Assert.DoesNotContain(email, uri.GetLeftPart(UriPartial.Path));
            Assert.DoesNotContain(email, uri.Query);
            Assert.DoesNotContain(code, uri.GetLeftPart(UriPartial.Path));
            Assert.DoesNotContain(code, uri.Query);
            Assert.Equal($"#email={Uri.EscapeDataString(email)}&code={code}", uri.Fragment);
        }
    }

}
