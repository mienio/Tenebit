using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using Tenebit.Application.Identity;

namespace Tenebit.Api.Auth;

public sealed class TokenIssuer
{
    private readonly IConfiguration _configuration;

    public TokenIssuer(IConfiguration configuration) => _configuration = configuration;

    public string Issue(AuthUserResponse user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new("organization_id", user.OrganizationId.ToString()),
            new("organization_name", user.OrganizationName),
            new("name", user.DisplayName),
            new("email", user.Email)
        };
        claims.AddRange(user.Roles.Select(role => new Claim("roles", role)));

        var credentials = new SigningCredentials(JwtSigningKey.Get(_configuration), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(12),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
