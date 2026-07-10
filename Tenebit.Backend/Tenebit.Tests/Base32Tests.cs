using System.Text;
using Tenebit.Application.Identity;

namespace Tenebit.Tests;

public class Base32Tests
{
    [Theory]
    [InlineData("")]
    [InlineData("f")]
    [InlineData("fo")]
    [InlineData("foo")]
    [InlineData("foob")]
    [InlineData("fooba")]
    [InlineData("foobar")]
    public void Encode_ThenDecode_RoundTrips(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var encoded = Base32.Encode(bytes);
        var decoded = Base32.Decode(encoded);
        Assert.Equal(bytes, decoded);
    }

    [Fact]
    public void Encode_UsesOnlyUppercaseRfc4648Alphabet()
    {
        var encoded = Base32.Encode(Encoding.UTF8.GetBytes("some random secret bytes"));
        Assert.Matches("^[A-Z2-7]+$", encoded);
    }
}
