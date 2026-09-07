namespace Tenebit.Api.Auth;

/// <summary>Own cookie name/path, isolated from <see cref="RefreshTokenCookie"/> (tenant) and any
/// admin session storage - so a browser holding both a client and a partner session never confuses one
/// refresh cookie for the other (spec §4.1/§10).</summary>
public static class AffiliateRefreshTokenCookie
{
    public const string CookieName = "tenebit_partner_refresh";

    public static void Append(HttpResponse response, string rawToken, bool isDevelopment)
    {
        response.Cookies.Append(CookieName, rawToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = !isDevelopment,
            SameSite = SameSiteMode.Lax,
            Path = "/api/partner",
            Expires = DateTimeOffset.UtcNow.AddDays(30)
        });
    }

    public static void Delete(HttpResponse response, bool isDevelopment)
    {
        response.Cookies.Delete(CookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = !isDevelopment,
            SameSite = SameSiteMode.Lax,
            Path = "/api/partner"
        });
    }
}
