using System.Net;
using System.Linq;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

internal static class TrustedForwardedHeadersConfiguration
{
    private const string KnownProxiesSection = "ReverseProxy:KnownProxies";

    public static IServiceCollection AddTrustedForwardedHeaders(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var configuredProxies = configuration
            .GetSection(KnownProxiesSection)
            .GetChildren()
            .Select(child => child.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray();

        if (environment.IsProduction() && configuredProxies.Length == 0)
        {
            throw new InvalidOperationException(
                "Production requires at least one exact ReverseProxy:KnownProxies address. " +
                "Kestrel must not trust forwarded headers from arbitrary peers.");
        }

        var parsedProxies = configuredProxies.Select(value =>
        {
            if (!IPAddress.TryParse(value, out var address))
            {
                throw new InvalidOperationException(
                    $"ReverseProxy:KnownProxies contains an invalid IP address: '{value}'.");
            }

            return address;
        }).ToArray();

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor |
                ForwardedHeaders.XForwardedProto |
                ForwardedHeaders.XForwardedHost;
            options.ForwardLimit = 1;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();

            foreach (var proxy in parsedProxies)
            {
                options.KnownProxies.Add(proxy);
            }
        });

        return services;
    }
}
