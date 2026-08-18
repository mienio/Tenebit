using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Tenebit.Infrastructure.Services;

internal static class ProductionSecurityConfiguration
{
    private const string CorsOriginsSection = "Cors:AllowedOrigins";

    public static IConfiguration ValidateProductionSecurityConfiguration(
        this IConfiguration configuration,
        IHostEnvironment environment)
    {
        if (!environment.IsProduction()) return configuration;

        var errors = new List<string>();
        var allowedHosts = configuration["AllowedHosts"];
        if (string.IsNullOrWhiteSpace(allowedHosts) || allowedHosts.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Any(x => x == "*"))
            errors.Add("Production AllowedHosts must contain explicit public hosts and cannot contain '*'.");

        var origins = configuration.GetSection(CorsOriginsSection).Get<string[]>() ?? [];
        if (origins.Length == 0 || origins.Any(origin => !Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo)))
            errors.Add($"Production {CorsOriginsSection} must contain explicit HTTPS origins only.");

        var publicUrlRaw = configuration["App:PublicUrl"];
        if (!Uri.TryCreate(publicUrlRaw, UriKind.Absolute, out var publicUrl) || publicUrl.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(publicUrl.UserInfo))
            errors.Add("Production App:PublicUrl must be an absolute HTTPS URL without user info.");
        else if (origins.Length > 0 && !origins.Any(x => string.Equals(x.TrimEnd('/'), publicUrl.GetLeftPart(UriPartial.Authority).TrimEnd('/'), StringComparison.OrdinalIgnoreCase)))
            errors.Add("App:PublicUrl origin must be present in Cors:AllowedOrigins.");

        var signingKey = configuration["Auth:SigningKey"];
        if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32 || signingKey == "tenebit-development-signing-key-change-me-32chars")
            errors.Add("Production Auth:SigningKey must be unique and at least 32 characters.");

        errors.AddRange(FieldEncryptionKeyRing.ValidateProduction(configuration, signingKey));

        var connectionString = configuration.GetConnectionString("TenebitDb") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(connectionString) || connectionString.Contains("Password=postgres", StringComparison.OrdinalIgnoreCase))
            errors.Add("Production database connection string is missing or uses the repository default password.");

        var stripeParts = new[] { configuration["Stripe:SecretKey"], configuration["Stripe:WebhookSecret"], configuration["Stripe:ProPriceId"] };
        if (stripeParts.Any(x => !string.IsNullOrWhiteSpace(x)) && stripeParts.Any(string.IsNullOrWhiteSpace))
            errors.Add("Stripe must be either fully disabled or configured with SecretKey, WebhookSecret and ProPriceId together.");

        var emailEnabled = configuration.GetValue("Email:Enabled", false);
        if (emailEnabled && (string.IsNullOrWhiteSpace(configuration["Email:Host"]) || string.IsNullOrWhiteSpace(configuration["Email:FromAddress"])))
            errors.Add("Email is enabled but SMTP host/from address is incomplete.");

        if (errors.Count > 0)
            throw new InvalidOperationException("Unsafe Production configuration: " + string.Join(" | ", errors));

        return configuration;
    }
}
