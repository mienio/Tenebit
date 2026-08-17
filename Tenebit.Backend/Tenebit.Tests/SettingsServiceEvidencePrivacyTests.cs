using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Application.Settings;
using Tenebit.Domain.Organizations;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class SettingsServiceEvidencePrivacyTests
{
    private static (SettingsService Service, FakeCurrentUser User, InMemoryOrganizationRepository Organizations, InMemoryActivityLogRepository Activity) CreateService()
    {
        var statusSettings = new InMemoryAssetStatusSettingRepository();
        var organizations = new InMemoryOrganizationRepository();
        var activity = new InMemoryActivityLogRepository();
        var currentUser = new FakeCurrentUser();
        var service = new SettingsService(statusSettings, organizations, activity, currentUser, new FakeClock(), new FakeUnitOfWork());
        return (service, currentUser, organizations, activity);
    }

    private static Organization AddOrganization(FakeCurrentUser user, InMemoryOrganizationRepository organizations)
    {
        var organization = Organization.CreateSeed(user.OrganizationId, "Acme", "PL", "pl", "PLN", "Europe/Warsaw");
        organizations.Add(organization);
        return organization;
    }

    [Fact]
    public async Task GetEvidencePrivacyAsync_ReturnsDefaultsForNewOrganization()
    {
        var (service, user, organizations, _) = CreateService();
        AddOrganization(user, organizations);

        var result = await service.GetEvidencePrivacyAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PublicIpCaptureMode.Off, result.Value!.CapturePublicIp);
        Assert.Null(result.Value!.DefaultEvidenceRetentionMonths);
    }

    [Fact]
    public async Task SaveEvidencePrivacyAsync_ForbiddenForNonPrivilegedRole()
    {
        var (service, user, organizations, _) = CreateService();
        AddOrganization(user, organizations);
        user.Roles = ["employee"];

        var result = await service.SaveEvidencePrivacyAsync(new SaveEvidencePrivacySettingsRequest(PublicIpCaptureMode.Off, null, null, null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("FORBIDDEN", result.Error!.Code);
    }

    [Fact]
    public async Task SaveEvidencePrivacyAsync_RejectsPublicIpCaptureEnabledWithoutRetentionDays()
    {
        var (service, user, organizations, _) = CreateService();
        AddOrganization(user, organizations);

        var result = await service.SaveEvidencePrivacyAsync(new SaveEvidencePrivacySettingsRequest(PublicIpCaptureMode.Truncated, null, null, null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("PRIVACY_IP_RETENTION_REQUIRED", result.Error!.Code);
    }

    [Fact]
    public async Task SaveEvidencePrivacyAsync_RejectsNonPositiveEvidenceRetentionMonths()
    {
        var (service, user, organizations, _) = CreateService();
        AddOrganization(user, organizations);

        var result = await service.SaveEvidencePrivacyAsync(new SaveEvidencePrivacySettingsRequest(PublicIpCaptureMode.Off, null, 0, null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("PRIVACY_EVIDENCE_RETENTION_MUST_BE_POSITIVE", result.Error!.Code);
    }

    [Fact]
    public async Task SaveEvidencePrivacyAsync_SucceedsAndWritesActivityLog()
    {
        var (service, user, organizations, activity) = CreateService();
        var organization = AddOrganization(user, organizations);

        var result = await service.SaveEvidencePrivacyAsync(new SaveEvidencePrivacySettingsRequest(PublicIpCaptureMode.Truncated, 30, 24, "https://acme.test/privacy", "privacy@acme.test"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PublicIpCaptureMode.Truncated, organization.CapturePublicIp);
        Assert.Equal(30, organization.PublicIpRetentionDays);
        Assert.Equal(24, organization.DefaultEvidenceRetentionMonths);
        Assert.Contains(activity.Logs, l => l.Action == "settings.evidence_privacy.updated" && l.EntityId == organization.Id);
    }
}
