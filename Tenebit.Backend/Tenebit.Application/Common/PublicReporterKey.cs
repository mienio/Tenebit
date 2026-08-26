using System.Security.Cryptography;
using System.Text;

namespace Tenebit.Application.Common;

/// <summary>
/// Turns a public reporter's IP address into a pseudonym usable as a rate-limit key.
///
/// A plain SHA-256 of an IP would be no protection at all: the whole IPv4 space is four billion
/// values, so anyone holding the table could recover every address in minutes. The derivation is
/// therefore deliberately slow and salted per organization, which puts an exhaustive sweep far out of
/// reach for anyone who does not already own the database - and even then, only for one tenant at a
/// time.
///
/// The cost lands on a public, rate-limited endpoint that a person hits by scanning a QR code, so a
/// few tens of milliseconds are invisible here and are themselves a brake on anyone hammering it.
/// </summary>
public static class PublicReporterKey
{
    private const int Iterations = 120_000;

    /// <summary>
    /// Every caller we cannot tell apart shares this bucket, which is the safe direction: with no
    /// address available the per-reporter limits collapse into one global limit rather than opening
    /// up. An organization that keeps IP capture switched off for privacy therefore gets a stricter
    /// rate limit, never a weaker one.
    /// </summary>
    public const string AnonymousBucket = "anonymous";

    public static string Derive(Guid organizationId, string? rawIp)
    {
        var normalized = Normalize(rawIp);
        if (normalized is null) return AnonymousBucket;

        var salt = SHA256.HashData(Encoding.UTF8.GetBytes($"tenebit-public-reporter-v1:{organizationId:N}"));
        var derived = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(normalized), salt, Iterations, HashAlgorithmName.SHA256, 32);
        return Convert.ToBase64String(derived);
    }

    private static string? Normalize(string? rawIp)
    {
        if (string.IsNullOrWhiteSpace(rawIp)) return null;
        // Parse and re-render so that "10.0.0.1", " 10.0.0.1 " and an IPv6-mapped form of the same
        // address cannot each buy their own quota.
        return System.Net.IPAddress.TryParse(rawIp.Trim(), out var address) ? address.ToString() : null;
    }
}
