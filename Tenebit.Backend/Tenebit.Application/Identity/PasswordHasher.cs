using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace Tenebit.Application.Identity;

public static class PasswordHasher
{
    private const string Argon2Prefix = "argon2id";
    private const int ArgonMemoryKb = 19_456;
    private const int ArgonIterations = 2;
    private const int ArgonParallelism = 1;
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int MinLegacyIterations = 10_000;
    private const int MaxLegacyIterations = 1_000_000;

    private static readonly SemaphoreSlim ArgonGate = new(Math.Clamp(Environment.ProcessorCount, 1, 4));
    private static readonly Lazy<string> DummyHashValue = new(() => Hash("tenebit-dummy-password-not-used"));
    public static string DummyHash => DummyHashValue.Value;

    public static string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = DeriveArgon2(password, salt, ArgonMemoryKb, ArgonIterations, ArgonParallelism, HashSize);
        return $"{Argon2Prefix}${ArgonMemoryKb}${ArgonIterations}${ArgonParallelism}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string? passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash)) return false;
        return passwordHash.StartsWith(Argon2Prefix + "$", StringComparison.Ordinal)
            ? VerifyArgon2(password, passwordHash)
            : VerifyLegacyPbkdf2(password, passwordHash);
    }

    public static bool NeedsRehash(string? passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash)) return true;
        var parts = passwordHash.Split('$');
        return parts.Length != 6
            || parts[0] != Argon2Prefix
            || !int.TryParse(parts[1], out var memory) || memory != ArgonMemoryKb
            || !int.TryParse(parts[2], out var iterations) || iterations != ArgonIterations
            || !int.TryParse(parts[3], out var parallelism) || parallelism != ArgonParallelism;
    }

    private static bool VerifyArgon2(string password, string encoded)
    {
        try
        {
            var parts = encoded.Split('$');
            if (parts.Length != 6 || parts[0] != Argon2Prefix) return false;
            if (!int.TryParse(parts[1], out var memoryKb) || memoryKb is < 4_096 or > 65_536) return false;
            if (!int.TryParse(parts[2], out var iterations) || iterations is < 1 or > 10) return false;
            if (!int.TryParse(parts[3], out var parallelism) || parallelism is < 1 or > 16) return false;

            var salt = Convert.FromBase64String(parts[4]);
            var expected = Convert.FromBase64String(parts[5]);
            if (salt.Length is < 8 or > 64 || expected.Length is < 16 or > 64) return false;

            var actual = DeriveArgon2(password, salt, memoryKb, iterations, parallelism, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool VerifyLegacyPbkdf2(string password, string encoded)
    {
        try
        {
            var parts = encoded.Split('.', 3);
            if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations) || iterations is < MinLegacyIterations or > MaxLegacyIterations)
                return false;

            var salt = Convert.FromBase64String(parts[1]);
            var expected = Convert.FromBase64String(parts[2]);
            if (salt.Length is < 8 or > 64 || expected.Length is < 16 or > 64) return false;

            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static byte[] DeriveArgon2(string password, byte[] salt, int memoryKb, int iterations, int parallelism, int outputBytes)
    {
        // Argon2 intentionally consumes memory. Bound concurrent derivations so a login burst cannot
        // turn that defensive cost into an avoidable process-wide memory spike on a small server.
        ArgonGate.Wait();
        try
        {
            using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
            {
                Salt = salt,
                MemorySize = memoryKb,
                Iterations = iterations,
                DegreeOfParallelism = parallelism
            };
            return argon2.GetBytes(outputBytes);
        }
        finally
        {
            ArgonGate.Release();
        }
    }
}
