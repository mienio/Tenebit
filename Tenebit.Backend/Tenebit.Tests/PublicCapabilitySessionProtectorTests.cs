using Microsoft.Extensions.Configuration;
using Tenebit.Infrastructure.Services;

namespace Tenebit.Tests;

public sealed class PublicCapabilitySessionProtectorTests
{
    private static PublicCapabilitySessionProtector Create() => new(new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Auth:SigningKey"] = "test-signing-key-for-capability-session-at-least-32-characters"
        })
        .Build());

    [Fact]
    public void ProtectedCookie_DoesNotExposeRawSecret_AndRoundTripsOnlyForPurpose()
    {
        var protector = Create();
        var now = DateTimeOffset.UtcNow;
        const string secret = "RAW_TEST_SECRET_very_sensitive";

        var protectedValue = protector.Protect("assignment", secret, now.AddMinutes(10));

        Assert.DoesNotContain(secret, protectedValue, StringComparison.Ordinal);
        Assert.Equal(secret, protector.Unprotect(protectedValue, "assignment", now));
        Assert.Null(protector.Unprotect(protectedValue, "offboarding", now));
    }

    [Fact]
    public void ProtectedCookie_Expires()
    {
        var protector = Create();
        var now = DateTimeOffset.UtcNow;
        var protectedValue = protector.Protect("assignment", "secret", now.AddMinutes(1));
        Assert.Null(protector.Unprotect(protectedValue, "assignment", now.AddMinutes(2)));
    }
}
