using Microsoft.Extensions.Configuration;
using Tenebit.Application.Abstractions;
using Tenebit.Infrastructure.Services;

namespace Tenebit.Tests;

public class FieldEncryptorTests
{
    private static FieldEncryptor CreateEncryptor() => new(new ConfigurationBuilder().Build());

    [Fact]
    public void Encrypt_ThenDecrypt_RoundTripsToOriginalPlaintext()
    {
        var encryptor = CreateEncryptor();
        var ciphertext = encryptor.Encrypt(FieldEncryptionPurposes.TotpSecret, "JBSWY3DPEHPK3PXP");

        Assert.NotEqual("JBSWY3DPEHPK3PXP", ciphertext);
        Assert.Equal("JBSWY3DPEHPK3PXP", encryptor.Decrypt(FieldEncryptionPurposes.TotpSecret, ciphertext));
    }

    [Fact]
    public void Decrypt_WithWrongPurpose_DoesNotReturnOriginalPlaintext()
    {
        var encryptor = CreateEncryptor();
        var ciphertext = encryptor.Encrypt(FieldEncryptionPurposes.TotpSecret, "SECRET-VALUE");

        Assert.Throws<System.Security.Cryptography.AuthenticationTagMismatchException>(
            () => encryptor.Decrypt(FieldEncryptionPurposes.LicenseKey, ciphertext));
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
