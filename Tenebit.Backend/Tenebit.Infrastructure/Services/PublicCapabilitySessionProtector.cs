using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Tenebit.Application.Abstractions;

namespace Tenebit.Infrastructure.Services;

/// <summary>
/// Short-lived, stateless capability session shared by all replicas. The original e-mail bearer token
/// is encrypted inside an HttpOnly cookie; it never appears in a request target after the one-time
/// fragment exchange. Every public operation still revalidates the original token hash/parent state.
/// </summary>
public sealed class PublicCapabilitySessionProtector : IPublicCapabilitySessionProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _key;

    public PublicCapabilitySessionProtector(IConfiguration configuration)
    {
        var signingKey = configuration["Auth:SigningKey"];
        if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32)
            throw new InvalidOperationException("Auth:SigningKey is required for capability sessions.");
        _key = HMACSHA256.HashData(Encoding.UTF8.GetBytes(signingKey), Encoding.UTF8.GetBytes("tenebit-public-capability-session-v1"));
    }

    public string Protect(string purpose, string rawToken, DateTimeOffset expiresAt)
    {
        if (string.IsNullOrWhiteSpace(purpose) || string.IsNullOrWhiteSpace(rawToken)) throw new ArgumentException("Capability purpose/token is required.");
        var payload = JsonSerializer.SerializeToUtf8Bytes(new Payload(purpose, rawToken, expiresAt.ToUnixTimeSeconds()));
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var tag = new byte[TagSize];
        var cipher = new byte[payload.Length];
        using (var aes = new AesGcm(_key, TagSize))
            aes.Encrypt(nonce, payload, cipher, tag, Encoding.UTF8.GetBytes("tenebit-cap-v1"));
        var packed = new byte[nonce.Length + tag.Length + cipher.Length];
        Buffer.BlockCopy(nonce, 0, packed, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, packed, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipher, 0, packed, nonce.Length + tag.Length, cipher.Length);
        return Base64UrlEncode(packed);
    }

    public string? Unprotect(string protectedSession, string expectedPurpose, DateTimeOffset now)
    {
        try
        {
            var packed = Base64UrlDecode(protectedSession);
            if (packed.Length <= NonceSize + TagSize) return null;
            var nonce = packed.AsSpan(0, NonceSize);
            var tag = packed.AsSpan(NonceSize, TagSize);
            var cipher = packed.AsSpan(NonceSize + TagSize);
            var plain = new byte[cipher.Length];
            using (var aes = new AesGcm(_key, TagSize))
                aes.Decrypt(nonce, cipher, tag, plain, Encoding.UTF8.GetBytes("tenebit-cap-v1"));
            var payload = JsonSerializer.Deserialize<Payload>(plain);
            if (payload is null || !string.Equals(payload.Purpose, expectedPurpose, StringComparison.Ordinal) || payload.ExpiresAt <= now.ToUnixTimeSeconds()) return null;
            return string.IsNullOrWhiteSpace(payload.RawToken) ? null : payload.RawToken;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or JsonException)
        {
            return null;
        }
    }

    private static string Base64UrlEncode(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        return Convert.FromBase64String(padded);
    }

    private sealed record Payload(string Purpose, string RawToken, long ExpiresAt);
}
