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
    [Fact]
    public void HashOneTimeCode_NormalizesEmailAndFormatting()
    {
        var expected = TokenHasher.HashOneTimeCode("person@example.test", "123456");
        var actual = TokenHasher.HashOneTimeCode(" Person@Example.Test ", "123 456");
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void HashOneTimeCode_IsScopedToEmailAddress()
    {
        var first = TokenHasher.HashOneTimeCode("first@example.test", "123456");
        var second = TokenHasher.HashOneTimeCode("second@example.test", "123456");
        Assert.NotEqual(first, second);
    }

}
