namespace Tenebit.Api.Auth;

public static class RefreshTokenCookie
{
    public const string CookieName = "tenebit_refresh";

    public static void Append(HttpResponse response, string rawToken, bool isDevelopment)
    {
        response.Cookies.Append(CookieName, rawToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = !isDevelopment,
            SameSite = SameSiteMode.Lax,
            Path = "/api/auth",
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
            Path = "/api/auth"
        });
    }
}
