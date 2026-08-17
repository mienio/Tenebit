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
        return $"{baseUrl}/accept/{Uri.EscapeDataString(rawToken)}";
    }

    public string BuildAssetScanLink(Guid organizationId, Guid assetId)
    {
        var baseUrl = (_configuration["App:PublicUrl"] ?? "http://localhost:5173").TrimEnd('/');
        return $"{baseUrl}/scan/{organizationId}/{assetId}";
    }

    public string BuildPasswordResetLink(string rawToken)
    {
        var baseUrl = (_configuration["App:PublicUrl"] ?? "http://localhost:5173").TrimEnd('/');
        return $"{baseUrl}/reset-password?token={Uri.EscapeDataString(rawToken)}";
    }

    public string BuildEmailVerificationLink(string rawToken)
    {
        var baseUrl = (_configuration["App:PublicUrl"] ?? "http://localhost:5173").TrimEnd('/');
        return $"{baseUrl}/verify-email?token={Uri.EscapeDataString(rawToken)}";
    }

    public string BuildOffboardingLink(string rawToken)
    {
        var baseUrl = (_configuration["App:PublicUrl"] ?? "http://localhost:5173").TrimEnd('/');
        return $"{baseUrl}/exit/{Uri.EscapeDataString(rawToken)}";
    }

    public string BuildAssetAuditLink(string rawToken)
    {
        var baseUrl = (_configuration["App:PublicUrl"] ?? "http://localhost:5173").TrimEnd('/');
        return $"{baseUrl}/audit/{Uri.EscapeDataString(rawToken)}";
    }

    public string BuildAppUrl(string relativePath)
    {
        var baseUrl = (_configuration["App:PublicUrl"] ?? "http://localhost:5173").TrimEnd('/');
        var safePath = IsSafeRelativePath(relativePath) ? relativePath : "/dashboard";
        return $"{baseUrl}{safePath}";
    }

    // Same "/" + not "//" shape check used by the OAuth return-path validator — a leading "//" is
    // still schema-relative and browsers/redirects can treat it as a cross-origin absolute URL.
    private static bool IsSafeRelativePath(string? path) =>
        !string.IsNullOrWhiteSpace(path) && path.StartsWith('/') && !path.StartsWith("//", StringComparison.Ordinal);
}
