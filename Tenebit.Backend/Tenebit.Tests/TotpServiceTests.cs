using System.Security.Cryptography;
using Tenebit.Application.Identity;

namespace Tenebit.Tests;

public class TotpServiceTests
{
    [Fact]
    public void GenerateSecret_ProducesValidBase32String()
    {
        var secret = TotpService.GenerateSecret();
        Assert.Matches("^[A-Z2-7]+$", secret);
        Assert.NotEmpty(Base32.Decode(secret));
    }

    [Fact]
    public void ValidateCode_AcceptsCodeComputedIndependentlyForCurrentTimeStep()
    {
        var secret = TotpService.GenerateSecret();
        var expectedCode = ComputeReferenceCode(secret, DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30);
        Assert.True(TotpService.ValidateCode(secret, expectedCode));
    }

    [Fact]
    public void ValidateCode_RejectsObviouslyWrongCode()
    {
        var secret = TotpService.GenerateSecret();
        var expectedCode = ComputeReferenceCode(secret, DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30);
        var wrongCode = expectedCode == "000000" ? "111111" : "000000";
        Assert.False(TotpService.ValidateCode(secret, wrongCode));
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("abcdef")]
    public void ValidateCode_RejectsMalformedCodes(string code)
    {
        var secret = TotpService.GenerateSecret();
        Assert.False(TotpService.ValidateCode(secret, code));
    }

    [Fact]
    public void BuildOtpAuthUri_ContainsSecretAndIssuer()
    {
        var secret = TotpService.GenerateSecret();
        var uri = TotpService.BuildOtpAuthUri(secret, "user@example.com", "Tenebit");
        Assert.StartsWith("otpauth://totp/", uri);
        Assert.Contains($"secret={secret}", uri);
        Assert.Contains("issuer=Tenebit", uri);
    }

    // Independent RFC 6238 reference implementation used only to cross-check TotpService's output.
    private static string ComputeReferenceCode(string secret, long counter)
    {
        var key = Base32.Decode(secret);
        var counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian) Array.Reverse(counterBytes);

        var hash = new HMACSHA1(key).ComputeHash(counterBytes);
        var offset = hash[^1] & 0x0F;
        var binaryCode = ((hash[offset] & 0x7F) << 24)
            | ((hash[offset + 1] & 0xFF) << 16)
            | ((hash[offset + 2] & 0xFF) << 8)
            | (hash[offset + 3] & 0xFF);

        return (binaryCode % 1_000_000).ToString().PadLeft(6, '0');
    }
}
