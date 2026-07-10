using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Tenebit.Api.Auth.OAuth;

public static class AppleClientSecretBuilder
{
    public static string Build(AppleOAuthOptions options)
    {
        var ecdsa = ECDsa.Create();
        var pem = options.PrivateKey.Contains("BEGIN PRIVATE KEY", StringComparison.Ordinal)
            ? options.PrivateKey
            : $"-----BEGIN PRIVATE KEY-----\n{options.PrivateKey}\n-----END PRIVATE KEY-----";
        ecdsa.ImportFromPem(pem);

        var key = new ECDsaSecurityKey(ecdsa) { KeyId = options.KeyId };
        var credentials = new SigningCredentials(key, SecurityAlgorithms.EcdsaSha256);

        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: options.TeamId,
            audience: "https://appleid.apple.com",
            claims: [new Claim(JwtRegisteredClaimNames.Sub, options.ClientId)],
            notBefore: now,
            expires: now.AddMinutes(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
