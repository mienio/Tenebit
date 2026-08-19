using System.Security.Cryptography;
using Tenebit.Application.Identity;

namespace Tenebit.Application.Common;

public static class PublicTokenService
{
    public sealed record GeneratedToken(string RawToken, string TokenHash);

    // Generates a new raw token + its hash. Return RawToken to the caller ONLY at generation
    // time (e.g. to embed in a one-time link/email) - never log it, never persist it.
    // Persist only TokenHash (plus caller-chosen ExpiresAt) on the owning entity.
    //
    // Also the mechanism for "regenerate": call Generate() again and OVERWRITE the owning
    // entity's TokenHash/ExpiresAt columns with the new values. Because only the current hash
    // is stored (no history table), the previous raw token can no longer verify once the
    // column is overwritten - this is what "invalidates the previous token" in practice.
    public static GeneratedToken Generate()
    {
        var rawToken = TokenHasher.NewRawToken();
        return new GeneratedToken(rawToken, TokenHasher.Hash(rawToken));
    }

    // Constant-time verification against stored state. Callers own ExpiresAt/RevokedAt columns
    // on their own entity (e.g. future OffboardingProcess, AssetAuditCampaign).
    public static bool Verify(
        string? rawToken,
        string? storedHash,
        DateTimeOffset expiresAt,
        DateTimeOffset? revokedAt,
        DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(rawToken) || string.IsNullOrEmpty(storedHash))
            return false;

        if (revokedAt is not null || now > expiresAt)
            return false;

        byte[] candidateBytes;
        byte[] storedBytes;
        try
        {
            candidateBytes = Convert.FromBase64String(TokenHasher.Hash(rawToken));
            storedBytes = Convert.FromBase64String(storedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(candidateBytes, storedBytes);
    }
}
