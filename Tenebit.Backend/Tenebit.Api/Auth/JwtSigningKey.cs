using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Tenebit.Api.Auth;

public static class JwtSigningKey
{
    public static SymmetricSecurityKey Get(IConfiguration configuration)
    {
        var key = configuration["Auth:SigningKey"] ?? "tenebit-development-signing-key-change-me-32chars";
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    }
}
