using System.Security.Cryptography;
using Tenebit.Application.Identity;

namespace Tenebit.Tests;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_ProducesArgon2idHashThatVerifiesAgainstOriginalPassword()
    {
        var hash = PasswordHasher.Hash("correct-horse-battery-staple");

        Assert.StartsWith("argon2id$", hash, StringComparison.Ordinal);
        Assert.True(PasswordHasher.Verify("correct-horse-battery-staple", hash));
        Assert.False(PasswordHasher.NeedsRehash(hash));
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

    [Fact]
    public void Verify_AcceptsLegacyPbkdf2HashAndMarksItForUpgrade()
    {
        const string password = "legacy-password";
        var salt = RandomNumberGenerator.GetBytes(16);
        var expected = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        var legacy = $"100000.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(expected)}";

        Assert.True(PasswordHasher.Verify(password, legacy));
        Assert.True(PasswordHasher.NeedsRehash(legacy));
    }

    [Fact]
    public void Verify_RejectsLegacyHashWithUnboundedIterationCount()
    {
        var encodedSalt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        var encodedHash = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        Assert.False(PasswordHasher.Verify("anything", $"2000000000.{encodedSalt}.{encodedHash}"));
    }
}
