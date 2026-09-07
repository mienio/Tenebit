namespace Tenebit.Application.Abstractions;

/// <summary>One-way, server-peppered hash for click tracking (spec §3.3/§12.11) - enough to
/// dedupe/rate-limit the same IP/UA against the same code without ever storing the raw value, and
/// unlike <see cref="IFieldEncryptor"/> there is deliberately no way back to the plaintext even with
/// the key, since nothing here ever needs the original IP/UA again.</summary>
public interface IAffiliateClickHasher
{
    string Hash(string value);
}
