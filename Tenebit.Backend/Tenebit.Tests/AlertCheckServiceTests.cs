using Tenebit.Application.Alerts;
using Tenebit.Application.Common;
using Tenebit.Domain.Alerts;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Identity;
using Tenebit.Domain.Organizations;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class AlertCheckServiceTests
{
    private static (AlertCheckService Service, Organization Organization, InMemoryAssetRepository Assets, InMemorySentAlertRepository SentAlerts, FakeEmailSender EmailSender, FakeClock Clock) CreateService()
    {
        var organizations = new InMemoryOrganizationRepository();
        var organization = new Organization("Acme", "PL", "pl", "PLN", "UTC");
        organizations.Add(organization);

        var users = new InMemoryOrganizationUserRepository();
        var admin = new OrganizationUser(organization.Id, "admin@acme.test", "Admin", true);
        admin.Update(admin.Email, admin.DisplayName, true, [TenebitRoles.Owner]);
        users.Add(admin);

        var assets = new InMemoryAssetRepository();
        var sentAlerts = new InMemorySentAlertRepository();
        var emailSender = new FakeEmailSender();
        var clock = new FakeClock { UtcNow = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero) };

        var service = new AlertCheckService(
            organizations,
            users,
            assets,
            new InMemoryAssignmentRepository(),
            new InMemoryProcedureRepository(),
            new InMemoryPersonRepository(),
            sentAlerts,
            emailSender,
            clock,
            new FakeUnitOfWork());

        return (service, organization, assets, sentAlerts, emailSender, clock);
    }

    private static Asset AddWarrantyAsset(Organization organization, InMemoryAssetRepository assets, DateOnly warrantyUntil)
    {
        var asset = new Asset(organization.Id, Guid.NewGuid(), "Laptop", $"AT-{Guid.NewGuid():N}"[..8]);
        asset.UpdateCore(asset.Name, asset.AssetTag, null, null, null, null, null, null, null, null, warrantyUntil, null);
        assets.Add(asset);
        return asset;
    }

    // Domain-level: SentAlert

    [Fact]
    public void MarkSent_SetsStatusSentAtAndClearsRetryState()
    {
        var alert = new SentAlert(Guid.NewGuid(), "warranty_7d", Guid.NewGuid(), "admin@acme.test", DateTimeOffset.UtcNow);

        var sentAt = DateTimeOffset.UtcNow;
        alert.MarkSent(sentAt);

        Assert.Equal(SentAlertStatus.Sent, alert.Status);
        Assert.Equal(sentAt, alert.SentAt);
        Assert.Null(alert.NextAttemptAt);
    }

    [Fact]
    public void MarkFailed_SetsStatusFailedAndTruncatesLastError()
    {
        var alert = new SentAlert(Guid.NewGuid(), "warranty_7d", Guid.NewGuid(), "admin@acme.test", DateTimeOffset.UtcNow);
        var attemptedAt = DateTimeOffset.UtcNow;
        var longError = new string('x', SentAlert.LastErrorMaxLength + 50);

        alert.MarkFailed(attemptedAt, longError, TimeSpan.FromHours(1));

        Assert.Equal(SentAlertStatus.Failed, alert.Status);
        Assert.Null(alert.SentAt);
        Assert.NotNull(alert.LastError);
        Assert.Equal(SentAlert.LastErrorMaxLength, alert.LastError!.Length);
        Assert.Equal(attemptedAt + TimeSpan.FromHours(1), alert.NextAttemptAt);
    }

    [Fact]
    public void CanRetry_FalseWhenAttemptCountAtMax()
    {
        var alert = new SentAlert(Guid.NewGuid(), "warranty_7d", Guid.NewGuid(), "admin@acme.test", DateTimeOffset.UtcNow);
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 5; i++)
        {
            alert.MarkFailed(now, "err", TimeSpan.Zero);
        }

        Assert.False(alert.CanRetry(5, now.AddDays(1)));
    }

    [Fact]
    public void CanRetry_FalseWhenNextAttemptInFuture()
    {
        var alert = new SentAlert(Guid.NewGuid(), "warranty_7d", Guid.NewGuid(), "admin@acme.test", DateTimeOffset.UtcNow);
        var now = DateTimeOffset.UtcNow;
        alert.MarkFailed(now, "err", TimeSpan.FromHours(1));

        Assert.False(alert.CanRetry(5, now));
    }

    [Fact]
    public void CanRetry_TrueWhenBelowMaxAndPastNextAttempt()
    {
        var alert = new SentAlert(Guid.NewGuid(), "warranty_7d", Guid.NewGuid(), "admin@acme.test", DateTimeOffset.UtcNow);
        var now = DateTimeOffset.UtcNow;
        alert.MarkFailed(now, "err", TimeSpan.FromHours(1));

        Assert.True(alert.CanRetry(5, now.AddHours(2)));
    }

    // Domain-level: Organization quiet hours

    [Fact]
    public void IsWithinQuietHours_FalseWhenNotConfigured()
    {
        var organization = new Organization("Acme", "PL", "pl", "PLN", "UTC");

        Assert.False(organization.IsWithinQuietHours(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void IsWithinQuietHours_TrueWithinNormalWindow()
    {
        var organization = new Organization("Acme", "PL", "pl", "PLN", "UTC");
        organization.SetQuietHours(new TimeOnly(22, 0), new TimeOnly(23, 0));

        var now = new DateTimeOffset(2026, 1, 1, 22, 30, 0, TimeSpan.Zero);

        Assert.True(organization.IsWithinQuietHours(now));
    }

    [Fact]
    public void IsWithinQuietHours_TrueWithinWrappingWindow()
    {
        var organization = new Organization("Acme", "PL", "pl", "PLN", "UTC");
        organization.SetQuietHours(new TimeOnly(22, 0), new TimeOnly(6, 0));

        var now = new DateTimeOffset(2026, 1, 1, 2, 0, 0, TimeSpan.Zero);

        Assert.True(organization.IsWithinQuietHours(now));
    }

    [Fact]
    public void IsWithinQuietHours_FalseOutsideWindow()
    {
        var organization = new Organization("Acme", "PL", "pl", "PLN", "UTC");
        organization.SetQuietHours(new TimeOnly(22, 0), new TimeOnly(23, 0));

        var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        Assert.False(organization.IsWithinQuietHours(now));
    }

    [Fact]
    public void SetQuietHours_ThrowsWhenOnlyOneValueGiven()
    {
        var organization = new Organization("Acme", "PL", "pl", "PLN", "UTC");

        Assert.Throws<Tenebit.Domain.Common.DomainException>(() => organization.SetQuietHours(new TimeOnly(22, 0), null));
    }

    // Service-level: warranty alert delivery, retry and quiet hours

    [Fact]
    public async Task RunAsync_SuccessfulSend_RecordsSentAndDoesNotResend()
    {
        var (service, organization, assets, sentAlerts, emailSender, clock) = CreateService();
        AddWarrantyAsset(organization, assets, DateOnly.FromDateTime(clock.UtcNow.UtcDateTime).AddDays(29));

        await service.RunAsync(3, CancellationToken.None);

        Assert.Single(sentAlerts.Alerts);
        Assert.Equal(SentAlertStatus.Sent, sentAlerts.Alerts[0].Status);
        Assert.Single(emailSender.Sent);

        await service.RunAsync(3, CancellationToken.None);

        Assert.Single(emailSender.Sent);
    }

    [Fact]
    public async Task RunAsync_SmtpFailure_RecordsFailedWithoutMarkingSent()
    {
        var (service, organization, assets, sentAlerts, emailSender, clock) = CreateService();
        AddWarrantyAsset(organization, assets, DateOnly.FromDateTime(clock.UtcNow.UtcDateTime).AddDays(29));
        emailSender.FailFor.Add("admin@acme.test");

        await service.RunAsync(3, CancellationToken.None);

        var record = Assert.Single(sentAlerts.Alerts);
        Assert.Equal(SentAlertStatus.Failed, record.Status);
        Assert.Null(record.SentAt);
        Assert.False(string.IsNullOrEmpty(record.LastError));
        Assert.Equal(1, record.AttemptCount);
        Assert.NotNull(record.NextAttemptAt);
        Assert.True(record.NextAttemptAt > clock.UtcNow);
        Assert.NotEqual(SentAlertStatus.Sent, record.Status);
    }

    [Fact]
    public async Task RunAsync_RetryAfterFailure_SendsOnceEligible()
    {
        var (service, organization, assets, sentAlerts, emailSender, clock) = CreateService();
        AddWarrantyAsset(organization, assets, DateOnly.FromDateTime(clock.UtcNow.UtcDateTime).AddDays(29));
        emailSender.FailFor.Add("admin@acme.test");

        await service.RunAsync(3, CancellationToken.None);

        clock.UtcNow = clock.UtcNow.AddHours(2);
        emailSender.FailFor.Remove("admin@acme.test");

        await service.RunAsync(3, CancellationToken.None);

        var record = Assert.Single(sentAlerts.Alerts);
        Assert.Equal(SentAlertStatus.Sent, record.Status);
        Assert.NotNull(record.SentAt);
        Assert.Equal(2, emailSender.AttemptCount);
    }

    [Fact]
    public async Task RunAsync_AttemptCap_StopsRetryingAfterMaxAttempts()
    {
        var (service, organization, assets, sentAlerts, emailSender, clock) = CreateService();
        AddWarrantyAsset(organization, assets, DateOnly.FromDateTime(clock.UtcNow.UtcDateTime).AddDays(29));
        emailSender.FailFor.Add("admin@acme.test");

        for (var i = 0; i < 5; i++)
        {
            await service.RunAsync(3, CancellationToken.None);
            clock.UtcNow = clock.UtcNow.AddHours(2);
        }

        var record = Assert.Single(sentAlerts.Alerts);
        Assert.Equal(SentAlertStatus.Failed, record.Status);
        Assert.Equal(5, record.AttemptCount);

        var attemptsSoFar = emailSender.AttemptCount;
        Assert.Equal(5, attemptsSoFar);

        await service.RunAsync(3, CancellationToken.None);

        Assert.Equal(SentAlertStatus.Failed, record.Status);
        Assert.Equal(5, record.AttemptCount);
        Assert.Equal(attemptsSoFar, emailSender.AttemptCount);
    }

    [Fact]
    public async Task RunAsync_QuietHours_DefersSendUntilWindowEnds()
    {
        var (service, organization, assets, sentAlerts, emailSender, clock) = CreateService();
        AddWarrantyAsset(organization, assets, DateOnly.FromDateTime(clock.UtcNow.UtcDateTime).AddDays(29));
        organization.SetQuietHours(new TimeOnly(11, 0), new TimeOnly(13, 0));

        await service.RunAsync(3, CancellationToken.None);

        Assert.Empty(emailSender.Sent);
        Assert.DoesNotContain(sentAlerts.Alerts, x => x.Status == SentAlertStatus.Failed);

        clock.UtcNow = clock.UtcNow.AddHours(3);

        await service.RunAsync(3, CancellationToken.None);

        Assert.Single(emailSender.Sent);
        var record = Assert.Single(sentAlerts.Alerts);
        Assert.Equal(SentAlertStatus.Sent, record.Status);
    }
}
