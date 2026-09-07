using Tenebit.Api.Auth;
using Tenebit.Application.Affiliates;

namespace Tenebit.Api.Endpoints;

/// <summary>
/// The one publicly reachable, unauthenticated affiliate route (spec §2/§6.2): a short-link redirect
/// that records a click and hands the visitor a cookie carrying an unguessable attribution token -
/// never the code itself, so the customer's browser never sees which affiliate account a code belongs
/// to. Deliberately outside <c>/api</c> (matches the short-link shape from the plan,
/// <c>https://teneb.it/r/{code}</c>) and outside every authentication scheme: this is a redirect, not
/// an API call.
/// </summary>
public static class RedirectEndpoints
{
    public const string AttributionCookieName = "tnb_aff";
    private static readonly TimeSpan AttributionCookieLifetime = TimeSpan.FromDays(30);

    public static void MapRedirectEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/r/{code}", async (
                string code, HttpContext http, AffiliateTrackingService tracking,
                Microsoft.Extensions.Hosting.IHostEnvironment env, CancellationToken cancellationToken) =>
            {
                var result = await tracking.RecordClickAsync(
                    code, http.Connection.RemoteIpAddress?.ToString(), http.Request.Headers.UserAgent.ToString(), cancellationToken);

                // An unknown/inactive code still redirects to the plain landing page instead of a 404 -
                // a dead affiliate link should not look like a broken site to whoever clicked it.
                if (result.IsSuccess)
                {
                    http.Response.Cookies.Append(AttributionCookieName, result.Value!.AttributionToken.ToString("N"), new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = !env.IsDevelopment(),
                        SameSite = SameSiteMode.Lax,
                        Path = "/",
                        Expires = DateTimeOffset.UtcNow.Add(AttributionCookieLifetime)
                    });
                }

                return Results.Redirect($"/?ref={Uri.EscapeDataString(code)}");
            })
            .AllowAnonymous()
            .RequireRateLimiting("affiliate-redirect")
            .WithTags("AffiliateRedirect");
    }
}
