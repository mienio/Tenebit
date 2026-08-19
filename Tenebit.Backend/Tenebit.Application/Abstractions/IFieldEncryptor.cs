namespace Tenebit.Application.Abstractions;

/// <summary>Symmetryczne szyfrowanie pojedynczych wartości przed zapisem do bazy (TOTP secret, klucz
/// licencyjny, wrażliwe pola własne aktywów) - audyt P1.4/P1.5: bez tego kompromitacja bazy (backup, insider,
/// SQL injection) ujawniała te dane w czystym tekście niezależnie od maskowania na poziomie API.</summary>
public interface IFieldEncryptor
{
    string Encrypt(string purpose, string plaintext);
    string Decrypt(string purpose, string ciphertext);
}

public sealed class FieldDecryptionException : Exception
{
    public FieldDecryptionException(string purpose, string message, Exception? innerException = null) : base(message, innerException) => Purpose = purpose;
    public string Purpose { get; }
}

/// <summary>Etykiety "purpose" separujące klucze derywowane dla różnych zastosowań - kompromitacja jednego
/// nie ujawnia klucza używanego dla pozostałych.</summary>
public static class FieldEncryptionPurposes
{
    public const string TotpSecret = "totp-secret-v1";
    public const string LicenseKey = "license-key-v1";
    public const string AssetSensitiveField = "asset-sensitive-field-v1";
}
