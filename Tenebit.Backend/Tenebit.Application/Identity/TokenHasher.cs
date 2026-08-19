using System.Security.Cryptography;
using System.Text;

namespace Tenebit.Application.Identity;

public static class TokenHasher
{
    public const int OneTimeCodeLength = 6;

    public static string NewRawToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string NewOneTimeCode() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    public static string Hash(string rawToken) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    public static string HashOneTimeCode(string email, string code)
    {
        const int iterations = 120_000;
        var salt = SHA256.HashData(Encoding.UTF8.GetBytes($"tenebit-one-time-code-v1:{NormalizeEmail(email)}"));
        var derived = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(NormalizeOneTimeCode(code)),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            32);
        return Convert.ToBase64String(derived);
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static string NormalizeOneTimeCode(string code)
    {
        var digits = new string(code.Where(char.IsDigit).ToArray());
        return digits.Length <= OneTimeCodeLength ? digits : digits[..OneTimeCodeLength];
    }
}
