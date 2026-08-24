using System.Net;

namespace Tenebit.Api.Auth;

// The single platform-admin account lives entirely in server-side environment variables (Admin__Email,
// Admin__PasswordHash, Admin__TotpSecret), never in the database - it cannot be created, promoted to, or
// leaked through any tenant-facing API, backup, or DB dump.
public static class AdminAccountOptions
{
    public static string? Email(IConfiguration configuration) => Normalize(configuration["Admin:Email"]);

    public static string? PasswordHash(IConfiguration configuration) => configuration["Admin:PasswordHash"];

    public static string? TotpSecret(IConfiguration configuration) => configuration["Admin:TotpSecret"];

    public static int TokenMinutes(IConfiguration configuration) =>
        int.TryParse(configuration["Admin:TokenMinutes"], out var minutes) && minutes is > 0 and <= 60 ? minutes : 20;

    public static bool IsConfigured(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(Email(configuration)) &&
        !string.IsNullOrWhiteSpace(PasswordHash(configuration)) &&
        !string.IsNullOrWhiteSpace(TotpSecret(configuration));

    /// <summary>
    /// Where alerts about admin sign-ins and moderation actions are sent. Falls back to the admin's own
    /// login address, but should ideally be a different mailbox: if the login address itself is
    /// compromised, an attacker could otherwise delete the very alerts warning about their access.
    /// </summary>
    public static string? AlertEmail(IConfiguration configuration) =>
        Normalize(configuration["Admin:AlertEmail"]) ?? Email(configuration);

    /// <summary>
    /// Optional network fence (Admin__AllowedIps, comma-separated). When set, the admin API answers only
    /// from these addresses - a stolen password plus a stolen TOTP seed is still useless from anywhere
    /// else. Empty means "any address", which is the default because a fixed office/home IP is not
    /// something every deployment has.
    /// </summary>
    public static IReadOnlyList<string> AllowedIps(IConfiguration configuration) =>
        (configuration["Admin:AllowedIps"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();

    public static bool IsIpAllowed(IConfiguration configuration, IPAddress? remoteIp)
    {
        var allowed = AllowedIps(configuration);
        if (allowed.Count == 0) return true;
        if (remoteIp is null) return false;

        // Compare parsed addresses rather than strings so ::ffff:203.0.113.5 and 203.0.113.5 match.
        var normalized = remoteIp.IsIPv4MappedToIPv6 ? remoteIp.MapToIPv4() : remoteIp;
        foreach (var entry in allowed)
        {
            if (!IPAddress.TryParse(entry, out var candidate)) continue;
            var candidateNormalized = candidate.IsIPv4MappedToIPv6 ? candidate.MapToIPv4() : candidate;
            if (candidateNormalized.Equals(normalized)) return true;
        }

        return false;
    }

    private static string? Normalize(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
}
