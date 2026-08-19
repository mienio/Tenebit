using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Tenebit.Api.Auth;

public sealed record JwtSigningKeyMaterial(string KeyId, SymmetricSecurityKey Key);

public static class JwtSigningKey
{
    private const string LegacyKeyId = "legacy";
    private const string DevelopmentKey = "tenebit-development-signing-key-change-me-32chars";

    public static JwtSigningKeyMaterial GetActive(IConfiguration configuration)
    {
        var keys = Load(configuration);
        var activeId = configuration["Auth:ActiveSigningKeyId"]?.Trim();
        if (!string.IsNullOrWhiteSpace(activeId))
        {
            var active = keys.FirstOrDefault(x => string.Equals(x.KeyId, activeId, StringComparison.Ordinal));
            if (active is null)
            {
                throw new InvalidOperationException($"Auth:ActiveSigningKeyId '{activeId}' does not exist in Auth:SigningKeys.");
            }

            return active;
        }

        var legacyId = configuration["Auth:SigningKeyId"]?.Trim();
        if (string.IsNullOrWhiteSpace(legacyId)) legacyId = LegacyKeyId;
        var legacy = keys.FirstOrDefault(x => string.Equals(x.KeyId, legacyId, StringComparison.Ordinal));
        if (legacy is not null) return legacy;

        if (keys.Count == 1) return keys[0];
        throw new InvalidOperationException("Configure Auth:ActiveSigningKeyId when more than one JWT signing key is available.");
    }

    public static IReadOnlyList<SecurityKey> GetValidationKeys(IConfiguration configuration, string? keyId)
    {
        var keys = Load(configuration);
        if (string.IsNullOrWhiteSpace(keyId))
        {
            // Tokens issued before key rotation support did not contain kid. Keep validating them against
            // all currently retained keys until their short access-token TTL has elapsed.
            return keys.Select(x => (SecurityKey)x.Key).ToArray();
        }

        var match = keys.FirstOrDefault(x => string.Equals(x.KeyId, keyId, StringComparison.Ordinal));
        return match is null ? [] : [match.Key];
    }

    public static IReadOnlyDictionary<string, string> GetConfiguredSecrets(IConfiguration configuration)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var child in configuration.GetSection("Auth:SigningKeys").GetChildren())
        {
            var value = child.Value?.Trim();
            if (!string.IsNullOrWhiteSpace(child.Key) && !string.IsNullOrWhiteSpace(value))
            {
                result[child.Key] = value;
            }
        }

        // Once an explicit key ring is configured it is authoritative. This matters in deployments
        // where appsettings.json still contains the development legacy SigningKey and production
        // injects Auth:SigningKeys through environment variables. Retain the old production key by
        // putting it in SigningKeys (for example under "legacy") during the rotation window.
        if (result.Count == 0)
        {
            var legacy = configuration["Auth:SigningKey"]?.Trim();
            if (!string.IsNullOrWhiteSpace(legacy))
            {
                var legacyId = configuration["Auth:SigningKeyId"]?.Trim();
                if (string.IsNullOrWhiteSpace(legacyId)) legacyId = LegacyKeyId;
                result.TryAdd(legacyId, legacy);
            }
        }

        if (result.Count == 0)
        {
            result[LegacyKeyId] = DevelopmentKey;
        }

        return result;
    }

    private static IReadOnlyList<JwtSigningKeyMaterial> Load(IConfiguration configuration) =>
        GetConfiguredSecrets(configuration)
            .Select(pair => new JwtSigningKeyMaterial(
                pair.Key,
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(pair.Value)) { KeyId = pair.Key }))
            .ToArray();
}
