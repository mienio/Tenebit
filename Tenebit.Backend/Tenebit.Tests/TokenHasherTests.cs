using Tenebit.Application.Identity;

namespace Tenebit.Tests;

public class TokenHasherTests
{
    [Fact]
    public void NewRawToken_ProducesDistinctUnpredictableValues()
    {
        var a = TokenHasher.NewRawToken();
        var b = TokenHasher.NewRawToken();
        Assert.NotEqual(a, b);
        Assert.True(a.Length >= 32);
    }

    [Fact]
    public void Hash_IsDeterministicForSameInput()
    {
        var token = TokenHasher.NewRawToken();
        Assert.Equal(TokenHasher.Hash(token), TokenHasher.Hash(token));
    }

    [Fact]
    public void Hash_DiffersForDifferentTokens()
    {
        var a = TokenHasher.NewRawToken();
        var b = TokenHasher.NewRawToken();
        Assert.NotEqual(TokenHasher.Hash(a), TokenHasher.Hash(b));
    }
}
