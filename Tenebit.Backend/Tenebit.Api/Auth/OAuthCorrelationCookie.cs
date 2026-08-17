namespace Tenebit.Api.Auth;

// Binds an OAuth transaction to the browser that started it: the raw value lives only in an
// HttpOnly cookie, and its hash is stored server-side against the state. Without this, a stolen
// or replayed callback URL lets an attacker complete someone else's browser session (login-CSRF /
// session swapping — audyt AUD3-005).
public static class OAuthCorrelationCookie
{
    public const string CookieName = "tenebit_oauth_correlation";

    public static void Append(HttpResponse response, string rawValue, bool isDevelopment, bool crossSitePost = false)
    {
        response.Cookies.Append(CookieName, rawValue, new CookieOptions
        {
            HttpOnly = true,
            // Apple's response_mode=form_post callback is a cross-site POST from appleid.apple.com —
            // SameSite=Lax cookies are not sent on cross-site POST, only on Lax-safe top-level GET
            // navigations (Google/Microsoft/Facebook use the GET redirect flow). Apple needs None+Secure.
            Secure = crossSitePost || !isDevelopment,
            SameSite = crossSitePost ? SameSiteMode.None : SameSiteMode.Lax,
            Path = "/api/auth/external",
            Expires = DateTimeOffset.UtcNow.AddMinutes(10)
        });
    }

    public static void Delete(HttpResponse response, bool isDevelopment)
    {
        response.Cookies.Delete(CookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = !isDevelopment,
            SameSite = SameSiteMode.Lax,
            Path = "/api/auth/external"
        });
    }
}
