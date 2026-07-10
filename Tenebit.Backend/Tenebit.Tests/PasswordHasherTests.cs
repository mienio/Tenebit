using Tenebit.Application.Identity;

namespace Tenebit.Tests;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_ProducesHashThatVerifiesAgainstOriginalPassword()
    {
        var hash = PasswordHasher.Hash("correct-horse-battery-staple");
        Assert.True(PasswordHasher.Verify("correct-horse-battery-staple", hash));
    }

    [Fact]
    public void Verify_RejectsWrongPassword()
    {
        var hash = PasswordHasher.Hash("correct-horse-battery-staple");
        Assert.False(PasswordHasher.Verify("wrong-password", hash));
    }

    [Fact]
    public void Verify_RejectsNullOrEmptyHash()
    {
        Assert.False(PasswordHasher.Verify("anything", null));
        Assert.False(PasswordHasher.Verify("anything", ""));
    }

    [Fact]
    public void Hash_ProducesDifferentOutputForSamePasswordDueToRandomSalt()
    {
        var hash1 = PasswordHasher.Hash("same-password");
        var hash2 = PasswordHasher.Hash("same-password");
        Assert.NotEqual(hash1, hash2);
        Assert.True(PasswordHasher.Verify("same-password", hash1));
        Assert.True(PasswordHasher.Verify("same-password", hash2));
    }
}
