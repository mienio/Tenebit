using Tenebit.Application.Identity;

namespace Tenebit.Api.Http;

public static class AuthRequestContextExtensions
{
    /// <summary>
    /// Captures who the sign-in attempt came from. RemoteIpAddress is rewritten by UseForwardedHeaders
    /// from the single trusted proxy hop, so unlike a raw X-Forwarded-For read it cannot be forged by the
    /// client. The user agent is attacker-controlled by nature and is stored purely as a display hint -
    /// LoginEvent truncates it and nothing branches on its value.
    /// </summary>
    public static AuthRequestContext ToAuthRequestContext(this HttpContext http) => new(
        http.Connection.RemoteIpAddress?.ToString(),
        http.Request.Headers.UserAgent.ToString() is { Length: > 0 } agent ? agent : null);
}
