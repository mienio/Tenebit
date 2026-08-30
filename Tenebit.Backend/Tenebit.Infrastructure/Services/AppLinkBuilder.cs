using Microsoft.Extensions.Configuration;
using Tenebit.Application.Abstractions;

namespace Tenebit.Infrastructure.Services;

public sealed class AppLinkBuilder : IAppLinkBuilder
{
    private readonly IConfiguration _configuration;

    public AppLinkBuilder(IConfiguration configuration) => _configuration = configuration;

    public string BuildAssignmentAcceptanceLink(string rawToken)
    {
        var baseUrl = (_configuration["App:PublicUrl"] ?? "http://localhost:5173").TrimEnd('/');
        return $"{baseUrl}/accept#{Uri.EscapeDataString(rawToken)}";
    }

    /// <summary>
    /// The address behind an asset's QR code, written to be as small as possible on paper.
    ///
    /// Three choices do the work, and none of them is the domain name. A ten-character random code
    /// replaces the two identifiers, upper case keeps the string in QR alphanumeric mode (5.5 bits per
    /// character instead of 8), and the short path leaves room for neither. Measured end to end that
    /// takes the code from 57x57 modules down to 33x33 - roughly 0.39 mm per module on a 63.5 mm label
    /// against 0.52 mm, and the difference between needing a second scan attempt and not.
    ///
    /// Note what does not help: a shorter host. Swapping app.tenebit.pl for teneb.it while the path still
    /// carried two lower-case GUIDs left the code at 57x57 exactly, because the identifiers were 73 of
    /// the 101 characters and lower-case hex forces byte mode however short the domain is.
    ///
    /// Case costs a reader nothing: scheme and host are case-insensitive by RFC 3986, and the code
    /// alphabet is upper-case by construction.
    /// </summary>
    public string BuildAssetScanLink(string scanCode)
    {
        var baseUrl = (_configuration["App:PublicUrl"] ?? "http://localhost:5173").TrimEnd('/');
        return $"{baseUrl}/s/{scanCode}".ToUpperInvariant();
    }

    public string BuildPasswordResetLink(string email, string code)
    {
        var baseUrl = (_configuration["App:PublicUrl"] ?? "http://localhost:5173").TrimEnd('/');
        return $"{baseUrl}/reset-password#email={Uri.EscapeDataString(email)}&code={Uri.EscapeDataString(code)}";
    }

    public string BuildEmailVerificationLink(string email, string code)
    {
        var baseUrl = (_configuration["App:PublicUrl"] ?? "http://localhost:5173").TrimEnd('/');
        return $"{baseUrl}/verify-email#email={Uri.EscapeDataString(email)}&code={Uri.EscapeDataString(code)}";
    }

    public string BuildOffboardingLink(string rawToken)
    {
        var baseUrl = (_configuration["App:PublicUrl"] ?? "http://localhost:5173").TrimEnd('/');
        return $"{baseUrl}/exit#{Uri.EscapeDataString(rawToken)}";
    }

    public string BuildAssetAuditLink(string rawToken)
    {
        var baseUrl = (_configuration["App:PublicUrl"] ?? "http://localhost:5173").TrimEnd('/');
        return $"{baseUrl}/audit#{Uri.EscapeDataString(rawToken)}";
    }

    public string BuildAppUrl(string relativePath)
    {
        var baseUrl = (_configuration["App:PublicUrl"] ?? "http://localhost:5173").TrimEnd('/');
        var safePath = IsSafeRelativePath(relativePath) ? relativePath : "/dashboard";
        return $"{baseUrl}{safePath}";
    }

    // Same "/" + not "//" shape check used by the OAuth return-path validator - a leading "//" is
    // still schema-relative and browsers/redirects can treat it as a cross-origin absolute URL.
    private static bool IsSafeRelativePath(string? path) =>
        !string.IsNullOrWhiteSpace(path) && path.StartsWith('/') && !path.StartsWith("//", StringComparison.Ordinal);
}
