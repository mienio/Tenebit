namespace Tenebit.Application.Abstractions;

/// <summary>
/// Server-side half of Cloudflare Turnstile ("I'm not a robot"): the widget in the browser produces a
/// one-time token, and this verifies that token against Cloudflare's siteverify API before an endpoint
/// trusts the request as human. See <c>ITurnstileVerifier</c> callers in AuthEndpoints/PartnerEndpoints.
/// </summary>
public interface ITurnstileVerifier
{
    /// <summary>False when no secret key is configured (local/dev) - callers should skip verification
    /// entirely rather than reject every request.</summary>
    bool IsConfigured { get; }

    Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken);
}
