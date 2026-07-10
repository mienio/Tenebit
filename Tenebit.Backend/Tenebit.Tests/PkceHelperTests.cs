using System.Security.Cryptography;
using System.Text;
using Tenebit.Api.Auth.OAuth;

namespace Tenebit.Tests;

public class PkceHelperTests
{
    [Fact]
    public void NewCodeVerifier_ProducesDistinctValuesOfSufficientLength()
    {
        var a = PkceHelper.NewCodeVerifier();
        var b = PkceHelper.NewCodeVerifier();
        Assert.NotEqual(a, b);
        Assert.InRange(a.Length, 43, 128);
    }

    [Fact]
    public void ChallengeFor_MatchesRfc7636Sha256Base64UrlDefinition()
    {
        var verifier = PkceHelper.NewCodeVerifier();
        var expected = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        Assert.Equal(expected, PkceHelper.ChallengeFor(verifier));
    }

    [Fact]
    public void NewState_ProducesUrlSafeDistinctValues()
    {
        var a = PkceHelper.NewState();
        var b = PkceHelper.NewState();
        Assert.NotEqual(a, b);
        Assert.DoesNotContain('+', a);
        Assert.DoesNotContain('/', a);
        Assert.DoesNotContain('=', a);
    }
}
