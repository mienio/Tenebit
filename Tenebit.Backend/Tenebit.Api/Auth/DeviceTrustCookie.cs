namespace Tenebit.Api.Auth;

public static class DeviceTrustCookie
{
    public const string CookieName = "tenebit_device_trust";

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
}
