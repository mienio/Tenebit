using System.Net;
using Tenebit.Domain.Organizations;

namespace Tenebit.Application.Common;

public sealed record PublicIpCapture(string? StoredIp, DateTimeOffset? ExpiresAt);

/// <summary>Single implementation of the organization's public-IP capture/retention contract.</summary>
public static class PublicIpPrivacyPolicy
{
    public static PublicIpCapture Capture(Organization organization, string? rawIp, DateTimeOffset capturedAt)
    {
        var stored = ApplyMode(organization.CapturePublicIp, rawIp);
        if (stored is null) return new PublicIpCapture(null, null);
        var expiresAt = organization.PublicIpRetentionDays is { } days ? capturedAt.AddDays(days) : (DateTimeOffset?)null;
        return new PublicIpCapture(stored, expiresAt);
    }

    public static string? ApplyMode(PublicIpCaptureMode mode, string? rawIp)
    {
        if (mode == PublicIpCaptureMode.Off || string.IsNullOrWhiteSpace(rawIp)) return null;
        if (!IPAddress.TryParse(rawIp.Trim(), out var address)) return null;
        if (mode == PublicIpCaptureMode.Full) return address.ToString();

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            bytes[3] = 0; // IPv4 /24
        }
        else
        {
            // IPv6 /56: preserve the first seven bytes and zero the host portion.
            for (var i = 7; i < bytes.Length; i++) bytes[i] = 0;
        }
        return new IPAddress(bytes).ToString();
    }
}
