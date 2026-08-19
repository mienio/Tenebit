using Tenebit.Application.Common;
using Tenebit.Domain.Organizations;

namespace Tenebit.Tests;

public sealed class PublicIpPrivacyPolicyTests
{
    private static Organization Organization(PublicIpCaptureMode mode, int? retentionDays)
    {
        var organization = new Organization("Acme", "PL", "pl", "PLN", "Europe/Warsaw");
        organization.UpdatePrivacySettings(mode, retentionDays, null, null, null);
        return organization;
    }

    [Fact]
    public void Capture_Off_DoesNotPersistRawIp()
    {
        var captured = PublicIpPrivacyPolicy.Capture(Organization(PublicIpCaptureMode.Off, null), "203.0.113.99", DateTimeOffset.UtcNow);
        Assert.Null(captured.StoredIp);
        Assert.Null(captured.ExpiresAt);
    }

    [Fact]
    public void Capture_Truncated_UsesIpv4Slash24AndRetention()
    {
        var now = new DateTimeOffset(2026, 8, 18, 8, 0, 0, TimeSpan.Zero);
        var captured = PublicIpPrivacyPolicy.Capture(Organization(PublicIpCaptureMode.Truncated, 14), "203.0.113.99", now);
        Assert.Equal("203.0.113.0", captured.StoredIp);
        Assert.Equal(now.AddDays(14), captured.ExpiresAt);
    }

    [Fact]
    public void Capture_Truncated_UsesIpv6Slash56()
    {
        var captured = PublicIpPrivacyPolicy.Capture(Organization(PublicIpCaptureMode.Truncated, 7), "2001:db8:abcd:12:3456:789a:bcde:f012", DateTimeOffset.UtcNow);
        Assert.Equal("2001:db8:abcd::", captured.StoredIp);
    }

    [Fact]
    public void Capture_Full_NormalizesAddress()
    {
        var captured = PublicIpPrivacyPolicy.Capture(Organization(PublicIpCaptureMode.Full, 30), "2001:0db8:0:0:0:0:0:1", DateTimeOffset.UtcNow);
        Assert.Equal("2001:db8::1", captured.StoredIp);
    }
}
