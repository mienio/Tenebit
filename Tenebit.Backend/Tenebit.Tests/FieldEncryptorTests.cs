using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Tenebit.Application.Abstractions;
using Tenebit.Infrastructure.Services;

namespace Tenebit.Tests;

public class FieldEncryptorTests
{
    private static FieldEncryptor CreateEncryptor() => new(new ConfigurationBuilder().Build(), NullLogger<FieldEncryptor>.Instance);

    [Fact]
    public void Encrypt_ThenDecrypt_RoundTripsToOriginalPlaintext()
    {
        var encryptor = CreateEncryptor();
        var ciphertext = encryptor.Encrypt(FieldEncryptionPurposes.TotpSecret, "JBSWY3DPEHPK3PXP");

        Assert.NotEqual("JBSWY3DPEHPK3PXP", ciphertext);
        Assert.Equal("JBSWY3DPEHPK3PXP", encryptor.Decrypt(FieldEncryptionPurposes.TotpSecret, ciphertext));
    }

    [Fact]
    public void Decrypt_WithWrongPurpose_ThrowsControlledError()
    {
        var encryptor = CreateEncryptor();
        var ciphertext = encryptor.Encrypt(FieldEncryptionPurposes.TotpSecret, "SECRET-VALUE");

        // Wrapped into a controlled, alertable exception instead of the raw crypto exception leaking out
        // as an unhandled 500 (audyt AUD3-011).
        Assert.Throws<InvalidOperationException>(
            () => encryptor.Decrypt(FieldEncryptionPurposes.LicenseKey, ciphertext));
    }

    [Fact]
    public void Decrypt_CorruptedCiphertext_ThrowsControlledError()
    {
        var encryptor = CreateEncryptor();

        Assert.Throws<InvalidOperationException>(
            () => encryptor.Decrypt(FieldEncryptionPurposes.TotpSecret, "v1:not-valid-base64!!"));
    }

    [Fact]
    public void Decrypt_ValuePersistedBeforeEncryptionWasIntroduced_IsReturnedUnchanged()
    {
        var encryptor = CreateEncryptor();
        Assert.Equal("legacy-plaintext-value", encryptor.Decrypt(FieldEncryptionPurposes.AssetSensitiveField, "legacy-plaintext-value"));
    }

    [Fact]
    public void Encrypt_SamePlaintextTwice_ProducesDifferentCiphertext()
    {
        var encryptor = CreateEncryptor();
        var a = encryptor.Encrypt(FieldEncryptionPurposes.LicenseKey, "SAME-VALUE");
        var b = encryptor.Encrypt(FieldEncryptionPurposes.LicenseKey, "SAME-VALUE");

        Assert.NotEqual(a, b);
    }
}
