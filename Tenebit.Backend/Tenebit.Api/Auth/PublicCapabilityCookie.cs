using Tenebit.Application.Abstractions;

namespace Tenebit.Api.Auth;

public static class PublicCapabilityCookie
{
    public const string AssignmentPurpose = "assignment";
    public const string OffboardingPurpose = "offboarding";
    public const string AssetAuditPurpose = "asset-audit";
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(2);

    public static string CookieName(string purpose) => purpose switch
    {
        AssignmentPurpose => "tenebit_cap_assignment",
        OffboardingPurpose => "tenebit_cap_offboarding",
        AssetAuditPurpose => "tenebit_cap_asset_audit",
        _ => throw new ArgumentOutOfRangeException(nameof(purpose))
    };

    public static void Issue(HttpResponse response, IPublicCapabilitySessionProtector protector, string purpose, string rawToken, DateTimeOffset now, bool isDevelopment)
    {
        var expiresAt = now.Add(SessionLifetime);
        response.Cookies.Append(CookieName(purpose), protector.Protect(purpose, rawToken, expiresAt), new CookieOptions
        {
            HttpOnly = true,
            Secure = !isDevelopment,
            SameSite = SameSiteMode.Strict,
            Path = "/api/public",
            MaxAge = SessionLifetime,
            IsEssential = true
        });
    }

    public static string? Read(HttpRequest request, IPublicCapabilitySessionProtector protector, string purpose, DateTimeOffset now)
    {
        return request.Cookies.TryGetValue(CookieName(purpose), out var protectedSession)
            ? protector.Unprotect(protectedSession, purpose, now)
            : null;
    }
}
