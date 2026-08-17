using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Tenebit.Application.Abstractions;

namespace Tenebit.Infrastructure.Services;

/// <summary>AES-256-GCM z kluczem derywowanym (HKDF) per-purpose z jednego sekretu konfiguracyjnego —
/// audyt P1.4/P1.5. Format: "v1:" + base64(nonce[12] + tag[16] + ciphertext). Wartości sprzed wdrożenia
/// szyfrowania (bez prefiksu "v1:") są zwracane bez zmian zamiast rzucać wyjątkiem przy odczycie.
/// Fallback do Auth:SigningKey/dev-secret jest wyłącznie dla lokalnego developmentu — Program.cs blokuje
/// start w Production bez odrębnego, silnego Auth:FieldEncryptionKey (audyt AUD3-011).</summary>
public sealed class FieldEncryptor : IFieldEncryptor
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const string FormatPrefix = "v1:";

    private readonly byte[] _rootKey;
    private readonly ILogger<FieldEncryptor> _logger;

    public FieldEncryptor(IConfiguration configuration, ILogger<FieldEncryptor> logger)
    {
        var secret = configuration["Auth:FieldEncryptionKey"]
            ?? configuration["Auth:SigningKey"]
            ?? "tenebit-development-field-encryption-key-change-me";
        _rootKey = Encoding.UTF8.GetBytes(secret);
        _logger = logger;
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

        try
        {
            var payload = Convert.FromBase64String(ciphertext[FormatPrefix.Length..]);
            if (payload.Length < NonceSize + TagSize)
            {
                throw new FormatException("Payload jest za krótki, by zawierać nonce i tag.");
            }

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
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            // Uszkodzony/nieprawidłowy ciphertext (zły klucz po rotacji, ręczna edycja w bazie) musi dać
            // jawny, alarmowalny błąd zamiast nieobsłużonego wyjątku kryptograficznego wypływającego jako
            // zwykły 500 bez kontekstu (audyt AUD3-011).
            _logger.LogError(ex, "Nie udało się odszyfrować pola o przeznaczeniu {Purpose} — ciphertext uszkodzony lub klucz szyfrowania się zmienił.", purpose);
            throw new InvalidOperationException($"Nie udało się odszyfrować pola '{purpose}'. Dane mogą być uszkodzone albo klucz szyfrowania uległ zmianie.", ex);
        }
    }

    private byte[] DeriveKey(string purpose) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256, _rootKey, outputLength: 32, info: Encoding.UTF8.GetBytes(purpose));
}
