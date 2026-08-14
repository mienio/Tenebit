using Tenebit.Domain.Common;
using Tenebit.Domain.Organizations;

namespace Tenebit.Tests;

public class OrganizationPrivacySettingsTests
{
    private static Organization CreateOrganization() => new("Acme", "PL", "pl", "PLN", "Europe/Warsaw");

    [Fact]
    public void UpdatePrivacySettings_DefaultsToCapturePublicIpOff()
    {
        var org = CreateOrganization();

        Assert.Equal(PublicIpCaptureMode.Off, org.CapturePublicIp);
        Assert.Null(org.PublicIpRetentionDays);
        Assert.Null(org.DefaultEvidenceRetentionMonths);
    }

    [Fact]
    public void UpdatePrivacySettings_ThrowsWhenCapturePublicIpEnabledWithoutRetentionDays()
    {
        var org = CreateOrganization();

        Assert.Throws<DomainException>(() => org.UpdatePrivacySettings(PublicIpCaptureMode.Truncated, null, null, null, null));
    }

    [Fact]
    public void UpdatePrivacySettings_ThrowsWhenCapturePublicIpEnabledWithZeroOrNegativeRetentionDays()
    {
        var org = CreateOrganization();

        Assert.Throws<DomainException>(() => org.UpdatePrivacySettings(PublicIpCaptureMode.Full, 0, null, null, null));
    }

    [Fact]
    public void UpdatePrivacySettings_ThrowsWhenEvidenceRetentionMonthsIsZeroOrNegative()
    {
        var org = CreateOrganization();

        Assert.Throws<DomainException>(() => org.UpdatePrivacySettings(PublicIpCaptureMode.Off, null, 0, null, null));
        Assert.Throws<DomainException>(() => org.UpdatePrivacySettings(PublicIpCaptureMode.Off, null, -1, null, null));
    }

    [Fact]
    public void UpdatePrivacySettings_SucceedsWithValidValues()
    {
        var org = CreateOrganization();

        org.UpdatePrivacySettings(PublicIpCaptureMode.Truncated, 30, 24, "https://acme.test/privacy", "privacy@acme.test");

        Assert.Equal(PublicIpCaptureMode.Truncated, org.CapturePublicIp);
        Assert.Equal(30, org.PublicIpRetentionDays);
        Assert.Equal(24, org.DefaultEvidenceRetentionMonths);
        Assert.Equal("https://acme.test/privacy", org.PrivacyNoticeUrl);
        Assert.Equal("privacy@acme.test", org.PrivacyContactEmail);
    }

    [Fact]
    public void UpdatePrivacySettings_ClearsRetentionDaysWhenCaptureIsTurnedOff()
    {
        var org = CreateOrganization();
        org.UpdatePrivacySettings(PublicIpCaptureMode.Full, 60, null, null, null);

        org.UpdatePrivacySettings(PublicIpCaptureMode.Off, null, null, null, null);

        Assert.Equal(PublicIpCaptureMode.Off, org.CapturePublicIp);
        Assert.Null(org.PublicIpRetentionDays);
    }
}
