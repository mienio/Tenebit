using Tenebit.Application.Common;

namespace Tenebit.Tests;

public class PublicTokenServiceTests
{
    [Fact]
    public void Generate_ProducesBase64UrlRawTokenOfAtLeast32Bytes()
    {
        var token = PublicTokenService.Generate();

        var padded = token.RawToken.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }

        var decoded = Convert.FromBase64String(padded);
        Assert.True(decoded.Length >= 32);
        Assert.NotEqual(token.TokenHash, token.RawToken);
    }

    [Fact]
    public void Generate_ProducesDistinctValuesEachCall()
    {
        var a = PublicTokenService.Generate();
        var b = PublicTokenService.Generate();

        Assert.NotEqual(a.RawToken, b.RawToken);
        Assert.NotEqual(a.TokenHash, b.TokenHash);
    }

    [Fact]
    public void Verify_ReturnsTrueForFreshValidToken()
    {
        var token = PublicTokenService.Generate();
        var now = DateTimeOffset.UtcNow;

        Assert.True(PublicTokenService.Verify(token.RawToken, token.TokenHash, now.AddHours(1), null, now));
    }

    [Fact]
    public void Verify_ReturnsFalseForWrongRawToken()
    {
        var a = PublicTokenService.Generate();
        var other = PublicTokenService.Generate();
        var now = DateTimeOffset.UtcNow;

        Assert.False(PublicTokenService.Verify(other.RawToken, a.TokenHash, now.AddHours(1), null, now));
    }

    [Fact]
    public void Verify_ReturnsFalseWhenExpired()
    {
        var token = PublicTokenService.Generate();
        var now = DateTimeOffset.UtcNow;

        Assert.False(PublicTokenService.Verify(token.RawToken, token.TokenHash, now.AddHours(-1), null, now));
    }

    [Fact]
    public void Verify_ReturnsFalseWhenRevoked()
    {
        var token = PublicTokenService.Generate();
        var now = DateTimeOffset.UtcNow;

        Assert.False(PublicTokenService.Verify(token.RawToken, token.TokenHash, now.AddHours(1), now.AddMinutes(-1), now));
    }

    [Theory]
    [InlineData(null, "hash")]
    [InlineData("", "hash")]
    [InlineData("raw", null)]
    [InlineData("raw", "")]
    public void Verify_ReturnsFalseForNullOrEmptyRawTokenOrStoredHash(string? rawToken, string? storedHash)
    {
        var now = DateTimeOffset.UtcNow;

        Assert.False(PublicTokenService.Verify(rawToken, storedHash, now.AddHours(1), null, now));
    }

    [Fact]
    public void Regenerate_InvalidatesPreviousToken()
    {
        var a = PublicTokenService.Generate();
        var now = DateTimeOffset.UtcNow;
        Assert.True(PublicTokenService.Verify(a.RawToken, a.TokenHash, now.AddHours(1), null, now));

        var b = PublicTokenService.Generate();

        Assert.False(PublicTokenService.Verify(a.RawToken, b.TokenHash, now.AddHours(1), null, now));
        Assert.True(PublicTokenService.Verify(b.RawToken, b.TokenHash, now.AddHours(1), null, now));
    }
}
