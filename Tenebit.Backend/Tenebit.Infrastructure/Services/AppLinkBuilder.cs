using Microsoft.Extensions.Configuration;
using Tenebit.Application.Abstractions;

namespace Tenebit.Infrastructure.Services;

public sealed class AppLinkBuilder : IAppLinkBuilder
{
    private readonly IConfiguration _configuration;

    public AppLinkBuilder(IConfiguration configuration) => _configuration = configuration;

    public string BuildAssignmentAcceptanceLink(Guid organizationId, Guid assignmentId)
    {
        var baseUrl = (_configuration["App:PublicUrl"] ?? "http://localhost:5173").TrimEnd('/');
        return $"{baseUrl}/accept/{organizationId}/{assignmentId}";
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
}
