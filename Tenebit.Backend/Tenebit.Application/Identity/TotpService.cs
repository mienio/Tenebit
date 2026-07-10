using System.Security.Cryptography;

namespace Tenebit.Application.Identity;

public static class TotpService
{
    private const int Digits = 6;
    private const int PeriodSeconds = 30;

    public static string GenerateSecret() => Base32.Encode(RandomNumberGenerator.GetBytes(20));

    public static string BuildOtpAuthUri(string secret, string email, string issuer = "Tenebit") =>
        $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(email)}?secret={secret}&issuer={Uri.EscapeDataString(issuer)}&digits={Digits}&period={PeriodSeconds}";

    public static bool ValidateCode(string secret, string code, int allowedDriftSteps = 1)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != Digits || !code.All(char.IsDigit))
        {
            return false;
        }

        var counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / PeriodSeconds;
        for (var drift = -allowedDriftSteps; drift <= allowedDriftSteps; drift++)
        {
            if (ComputeCode(secret, counter + drift) == code)
            {
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

        var hash = new HMACSHA1(key).ComputeHash(counterBytes);
        var offset = hash[^1] & 0x0F;
        var binaryCode = ((hash[offset] & 0x7F) << 24)
            | ((hash[offset + 1] & 0xFF) << 16)
            | ((hash[offset + 2] & 0xFF) << 8)
            | (hash[offset + 3] & 0xFF);

        var code = binaryCode % (int)Math.Pow(10, Digits);
        return code.ToString().PadLeft(Digits, '0');
    }
}
