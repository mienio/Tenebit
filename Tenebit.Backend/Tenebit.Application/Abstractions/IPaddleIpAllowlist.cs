using System.Net;

namespace Tenebit.Application.Abstractions;

/// <summary>
/// Network-level guard for the Paddle webhook: Paddle publishes the addresses it delivers from, and
/// anything else reaching our endpoint has no business being there.
///
/// This is defence in depth, not the actual authentication - the Paddle-Signature HMAC check in
/// <see cref="IPaymentGateway.ParseWebhook"/> is what proves a payload is genuine, and it stays the
/// control we rely on. The allowlist exists so forged bodies are dropped before they are parsed at all.
/// </summary>
public interface IPaddleIpAllowlist
{
    /// <summary>
    /// False only when the address is known not to be Paddle's. Deliberately true while the published list
    /// has never been fetched successfully (startup, or Paddle's endpoint being down): the signature check
    /// still stands behind this, and silently dropping real subscription events - which Paddle retries for
    /// a limited window and then gives up on - would cost paying customers their access over what is only a
    /// secondary control.
    /// </summary>
    bool IsAllowed(IPAddress? remoteIp);
}
