using System.Security.Cryptography;
using System.Text;

namespace Tenebit.Application.Identity;

public static class TotpService
{
    private const int Digits = 6;
    private const int PeriodSeconds = 30;

    public static string GenerateSecret() => Base32.Encode(RandomNumberGenerator.GetBytes(20));

    public static string BuildOtpAuthUri(string secret, string email, string issuer = "Tenebit") =>
        $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(email)}?secret={secret}&issuer={Uri.EscapeDataString(issuer)}&digits={Digits}&period={PeriodSeconds}";

    public static bool ValidateCode(string secret, string code, int allowedDriftSteps = 1) =>
        TryValidateCode(secret, code, out _, allowedDriftSteps);

    public static bool TryValidateCode(string secret, string code, out long matchedCounter, int allowedDriftSteps = 1)
    {
        matchedCounter = 0;
        if (string.IsNullOrWhiteSpace(code) || code.Length != Digits || !code.All(char.IsDigit)) return false;
        if (allowedDriftSteps is < 0 or > 5) return false;

        var counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / PeriodSeconds;
        var submitted = Encoding.ASCII.GetBytes(code);
        for (var drift = -allowedDriftSteps; drift <= allowedDriftSteps; drift++)
        {
            var candidateCounter = counter + drift;
            var candidate = Encoding.ASCII.GetBytes(ComputeCode(secret, candidateCounter));
            if (CryptographicOperations.FixedTimeEquals(candidate, submitted))
            {
                matchedCounter = candidateCounter;
                return true;
            }
        }

        return false;
    }

    private static string ComputeCode(string secret, long counter)
    {
        var key = Base32.Decode(secret);
        var counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian) Array.Reverse(counterBytes);

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(counterBytes);
        var offset = hash[^1] & 0x0F;
        var binaryCode = ((hash[offset] & 0x7F) << 24)
            | ((hash[offset + 1] & 0xFF) << 16)
            | ((hash[offset + 2] & 0xFF) << 8)
            | (hash[offset + 3] & 0xFF);

        var numericCode = binaryCode % 1_000_000;
        return numericCode.ToString().PadLeft(Digits, '0');
    }
}
