using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Tenebit.Application.Abstractions;

namespace Tenebit.Infrastructure.Services;

public sealed class TurnstileVerifier : ITurnstileVerifier
{
    private const int MaxResponseBytes = 64 * 1024;
    private readonly HttpClient _http;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TurnstileVerifier> _logger;

    public TurnstileVerifier(HttpClient http, IConfiguration configuration, ILogger<TurnstileVerifier> logger)
    {
        _http = http;
        _http.BaseAddress ??= new Uri("https://challenges.cloudflare.com/");
        _http.Timeout = TimeSpan.FromSeconds(10);
        _configuration = configuration;
        _logger = logger;
    }

    private string? SecretKey => _configuration["Turnstile:SecretKey"];

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SecretKey);

    public async Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken)
    {
        if (!IsConfigured) return true;
        if (string.IsNullOrWhiteSpace(token)) return false;

        var secretKey = SecretKey!;
        var fields = new Dictionary<string, string> { ["secret"] = secretKey, ["response"] = token };
        if (!string.IsNullOrWhiteSpace(remoteIp)) fields["remoteip"] = remoteIp;

        try
        {
            using var response = await _http.PostAsync("turnstile/v0/siteverify", new FormUrlEncodedContent(fields), cancellationToken);
            if (!response.IsSuccessStatusCode) return false;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var limited = new MemoryStream();
            await stream.CopyToAsync(limited, cancellationToken);
            if (limited.Length > MaxResponseBytes) return false;
            limited.Position = 0;

            var payload = await JsonSerializer.DeserializeAsync<SiteVerifyResponse>(limited, cancellationToken: cancellationToken);
            return payload?.Success ?? false;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Cloudflare being unreachable/slow must not silently let bots through, so treat it as a
            // failed verification (the user gets "try again") rather than swallowing it as configured.
            _logger.LogWarning(ex, "Turnstile siteverify call failed");
            return false;
        }
    }

    private sealed record SiteVerifyResponse([property: JsonPropertyName("success")] bool Success);
}
