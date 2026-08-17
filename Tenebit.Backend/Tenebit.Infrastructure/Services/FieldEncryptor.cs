using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Tenebit.Application.Abstractions;

namespace Tenebit.Infrastructure.Services;

/// <summary>AES-256-GCM z kluczem derywowanym (HKDF) per-purpose z jednego sekretu konfiguracyjnego —
/// audyt P1.4/P1.5. Format: "v1:" + base64(nonce[12] + tag[16] + ciphertext). Wartości sprzed wdrożenia
/// szyfrowania (bez prefiksu "v1:") są zwracane bez zmian zamiast rzucać wyjątkiem przy odczycie.</summary>
public sealed class FieldEncryptor : IFieldEncryptor
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const string FormatPrefix = "v1:";

    private readonly byte[] _rootKey;

    public FieldEncryptor(IConfiguration configuration)
    {
        var secret = configuration["Auth:FieldEncryptionKey"]
            ?? configuration["Auth:SigningKey"]
            ?? "tenebit-development-field-encryption-key-change-me";
        _rootKey = Encoding.UTF8.GetBytes(secret);
    }

    public string Encrypt(string purpose, string plaintext)
    {
        var key = DeriveKey(purpose);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        using (var aes = new AesGcm(key, TagSize))
        {
            aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);
        }

        var payload = new byte[NonceSize + TagSize + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, payload, NonceSize, TagSize);
        Buffer.BlockCopy(ciphertext, 0, payload, NonceSize + TagSize, ciphertext.Length);
        return FormatPrefix + Convert.ToBase64String(payload);
    }

    public string Decrypt(string purpose, string ciphertext)
    {
        if (!ciphertext.StartsWith(FormatPrefix, StringComparison.Ordinal))
        {
            return ciphertext;
        }

        var payload = Convert.FromBase64String(ciphertext[FormatPrefix.Length..]);
        var nonce = payload.AsSpan(0, NonceSize);
        var tag = payload.AsSpan(NonceSize, TagSize);
        var cipherBytes = payload.AsSpan(NonceSize + TagSize);
        var plaintextBytes = new byte[cipherBytes.Length];

        var key = DeriveKey(purpose);
        using (var aes = new AesGcm(key, TagSize))
        {
            aes.Decrypt(nonce, cipherBytes, tag, plaintextBytes);
        }

        return Encoding.UTF8.GetString(plaintextBytes);
    }

    private byte[] DeriveKey(string purpose) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256, _rootKey, outputLength: 32, info: Encoding.UTF8.GetBytes(purpose));
}
