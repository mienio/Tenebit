using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Tenebit.Application.Abstractions;
using Tenebit.Infrastructure.Services;

namespace Tenebit.Tests;

public sealed class FieldEncryptorTests
{
    private static IConfiguration Config(string active, bool allowPlaintext, params (string Id, string Key)[] keys)
    {
        var values = new Dictionary<string, string?>
        {
            ["Auth:FieldEncryption:ActiveKeyId"] = active,
            ["Auth:FieldEncryption:LegacyV1KeyId"] = keys[0].Id,
            ["Auth:FieldEncryption:AllowLegacyPlaintext"] = allowPlaintext.ToString()
        };
        foreach (var (id, key) in keys) values[$"Auth:FieldEncryption:Keys:{id}"] = key;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Fact]
    public void NewWritesUseActiveKeyId_AndRoundTrip()
    {
        var encryptor = new FieldEncryptor(Config("k2", false,
            ("k1", "old-field-key-abcdefghijklmnopqrstuvwxyz-123456"),
            ("k2", "new-field-key-abcdefghijklmnopqrstuvwxyz-123456")), NullLogger<FieldEncryptor>.Instance);
        var ciphertext = encryptor.Encrypt(FieldEncryptionPurposes.TotpSecret, "JBSWY3DPEHPK3PXP");
        Assert.StartsWith("v2:k2:", ciphertext);
        Assert.Equal("JBSWY3DPEHPK3PXP", encryptor.Decrypt(FieldEncryptionPurposes.TotpSecret, ciphertext));
    }

    [Fact]
    public void OldCiphertextDecryptsAfterKeyRotation()
    {
        const string oldKey = "old-field-key-abcdefghijklmnopqrstuvwxyz-123456";
        const string newKey = "new-field-key-abcdefghijklmnopqrstuvwxyz-123456";
        var before = new FieldEncryptor(Config("k1", false, ("k1", oldKey)), NullLogger<FieldEncryptor>.Instance);
        var ciphertext = before.Encrypt(FieldEncryptionPurposes.LicenseKey, "LICENSE-SECRET");
        var after = new FieldEncryptor(Config("k2", false, ("k1", oldKey), ("k2", newKey)), NullLogger<FieldEncryptor>.Instance);
        Assert.Equal("LICENSE-SECRET", after.Decrypt(FieldEncryptionPurposes.LicenseKey, ciphertext));
    }

    [Fact]
    public void WrongPurposeAndCorruptedCiphertext_ReturnControlledException()
    {
        var encryptor = new FieldEncryptor(Config("k1", false, ("k1", "field-key-abcdefghijklmnopqrstuvwxyz-123456789")), NullLogger<FieldEncryptor>.Instance);
        var ciphertext = encryptor.Encrypt(FieldEncryptionPurposes.TotpSecret, "SECRET");
        Assert.Throws<FieldDecryptionException>(() => encryptor.Decrypt(FieldEncryptionPurposes.LicenseKey, ciphertext));
        Assert.Throws<FieldDecryptionException>(() => encryptor.Decrypt(FieldEncryptionPurposes.TotpSecret, "v2:k1:not-base64!!"));
    }

    [Fact]
    public void LegacyPlaintext_IsOnlyAcceptedWhenExplicitlyEnabled()
    {
        const string key = "field-key-abcdefghijklmnopqrstuvwxyz-123456789";
        var migrationMode = new FieldEncryptor(Config("k1", true, ("k1", key)), NullLogger<FieldEncryptor>.Instance);
        var strictMode = new FieldEncryptor(Config("k1", false, ("k1", key)), NullLogger<FieldEncryptor>.Instance);
        Assert.Equal("legacy", migrationMode.Decrypt(FieldEncryptionPurposes.AssetSensitiveField, "legacy"));
        Assert.Throws<FieldDecryptionException>(() => strictMode.Decrypt(FieldEncryptionPurposes.AssetSensitiveField, "legacy"));
    }

    [Fact]
    public void ProductionValidationRejectsSigningKeyReuseAndPlaintextMode()
    {
        const string key = "same-key-abcdefghijklmnopqrstuvwxyz-123456789";
        var config = Config("k1", true, ("k1", key));
        var errors = FieldEncryptionKeyRing.ValidateProduction(config, key);
        Assert.NotEmpty(errors);
    }
}
