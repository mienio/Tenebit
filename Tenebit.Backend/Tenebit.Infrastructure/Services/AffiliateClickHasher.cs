using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Tenebit.Application.Abstractions;

namespace Tenebit.Infrastructure.Services;

/// <summary>Reuses the already-managed field-encryption key ring as HMAC key material (same
/// operational story as every other secret in this app: one key ring, rotated the same way) instead of
/// introducing a brand new secret just for click hashing. The purpose label keeps this derived key
/// cryptographically separate from the reversible encryption keys the ring is otherwise used for.</summary>
public sealed class AffiliateClickHasher : IAffiliateClickHasher
{
    private const string Purpose = "affiliate-click-hash-v1";
    private readonly byte[] _key;

    public AffiliateClickHasher(IConfiguration configuration)
    {
        var ring = FieldEncryptionKeyRing.Load(configuration);
        using var kdf = new HMACSHA256(ring.GetActiveKey());
        _key = kdf.ComputeHash(Encoding.UTF8.GetBytes(Purpose));
    }

    public string Hash(string value)
    {
        using var hmac = new HMACSHA256(_key);
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(value)));
    }
}
