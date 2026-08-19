using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;

namespace Tenebit.Infrastructure.Services;

public sealed class FieldEncryptor : IFieldEncryptor
{
    private const int NonceSize=12; private const int TagSize=16;
    private readonly FieldEncryptionKeyRing _ring; private readonly ILogger<FieldEncryptor> _logger;
    public FieldEncryptor(IConfiguration configuration,ILogger<FieldEncryptor> logger){_ring=FieldEncryptionKeyRing.Load(configuration);_logger=logger;}

    public string Encrypt(string purpose,string plaintext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose); ArgumentNullException.ThrowIfNull(plaintext);
        var keyId=_ring.ActiveKeyId; var key=Derive(_ring.GetActiveKey(),purpose); var nonce=RandomNumberGenerator.GetBytes(NonceSize);
        var plain=Encoding.UTF8.GetBytes(plaintext); var cipher=new byte[plain.Length]; var tag=new byte[TagSize];
        using(var aes=new AesGcm(key,TagSize)) aes.Encrypt(nonce,plain,cipher,tag,Aad(purpose,keyId));
        var payload=new byte[nonce.Length+tag.Length+cipher.Length]; Buffer.BlockCopy(nonce,0,payload,0,nonce.Length); Buffer.BlockCopy(tag,0,payload,nonce.Length,tag.Length); Buffer.BlockCopy(cipher,0,payload,nonce.Length+tag.Length,cipher.Length);
        return $"v2:{keyId}:{Convert.ToBase64String(payload)}";
    }

    public string Decrypt(string purpose,string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose); ArgumentNullException.ThrowIfNull(value);
        try
        {
            if(value.StartsWith("v2:",StringComparison.Ordinal))
            {
                var split=value.IndexOf(':',3); if(split<=3) throw new FormatException("Malformed v2 header."); var id=value[3..split];
                if(!_ring.TryGetKey(id,out var root)) throw new CryptographicException($"Unknown field-encryption key id '{id}'.");
                return DecryptPayload(Derive(root,purpose),value[(split+1)..],Aad(purpose,id));
            }
            if(value.StartsWith("v1:",StringComparison.Ordinal))
            {
                if(string.IsNullOrWhiteSpace(_ring.LegacyV1KeyId)||!_ring.TryGetKey(_ring.LegacyV1KeyId,out var root)) throw new CryptographicException("Legacy v1 key is unavailable.");
                return DecryptPayload(Derive(root,purpose),value[3..],null);
            }
            if(_ring.AllowLegacyPlaintext) return value;
            throw new CryptographicException("Legacy plaintext is disabled.");
        }
        catch(Exception ex) when(ex is FormatException or CryptographicException)
        {
            SecurityTelemetry.EncryptionFailure();
            _logger.LogError(ex,"Field decryption failed for purpose {Purpose}; ciphertext or key ring requires operator attention.",purpose);
            throw new FieldDecryptionException(purpose,"Encrypted value cannot be decrypted with the configured key ring.",ex);
        }
    }

    private static string DecryptPayload(byte[] key,string encoded,byte[]? aad){var payload=Convert.FromBase64String(encoded); if(payload.Length<NonceSize+TagSize)throw new FormatException("Encrypted payload too short."); var plain=new byte[payload.Length-NonceSize-TagSize]; using(var aes=new AesGcm(key,TagSize)) aes.Decrypt(payload.AsSpan(0,NonceSize),payload.AsSpan(NonceSize+TagSize),payload.AsSpan(NonceSize,TagSize),plain,aad); return Encoding.UTF8.GetString(plain);}
    private static byte[] Derive(byte[] root,string purpose)=>HKDF.DeriveKey(HashAlgorithmName.SHA256,root,32,info:Encoding.UTF8.GetBytes(purpose));
    private static byte[] Aad(string purpose,string id)=>Encoding.UTF8.GetBytes($"tenebit-field|v2|{id}|{purpose}");
}
