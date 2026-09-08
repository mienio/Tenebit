using Tenebit.Application.Abstractions;

namespace Tenebit.Api.Http;

public static class TurnstileEndpointExtensions
{
    /// <summary>
    /// Runs before the abuse-limited handler body: null means "proceed", any other IResult is the
    /// response to return as-is. Skips verification entirely when ITurnstileVerifier.IsConfigured is
    /// false, so local/dev environments without a Turnstile secret configured are unaffected.
    /// </summary>
    public static async Task<IResult?> VerifyTurnstileAsync(
        this HttpContext http, ITurnstileVerifier verifier, string? token, CancellationToken cancellationToken)
    {
        if (!verifier.IsConfigured) return null;

        var remoteIp = http.Connection.RemoteIpAddress?.ToString();
        if (await verifier.VerifyAsync(token, remoteIp, cancellationToken)) return null;

        return Results.Json(
            new ErrorResponse(ResultExtensions.Localize("Weryfikacja \"nie jestem robotem\" nie powiodła się. Spróbuj ponownie."), "CAPTCHA_FAILED"),
            statusCode: 400);
    }
}
