using Tenebit.Application.Alerts;
using Tenebit.Application.Common;
using Tenebit.Domain.Alerts;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class AlertSettingsServiceTests
{
    private static (AlertSettingsService Service, FakeCurrentUser User, InMemoryAlertRuleRepository Rules, InMemoryAlertDigestSettingsRepository Digest, InMemorySentAlertRepository SentAlerts, FakeEmailSender EmailSender) CreateService()
    {
        var rules = new InMemoryAlertRuleRepository();
        var digest = new InMemoryAlertDigestSettingsRepository();
        var sentAlerts = new InMemorySentAlertRepository();
        var emailSender = new FakeEmailSender();
        var user = new FakeCurrentUser();
        var clock = new FakeClock();
        var service = new AlertSettingsService(rules, digest, sentAlerts, emailSender, clock, user, new FakeUnitOfWork());
        return (service, user, rules, digest, sentAlerts, emailSender);
    }

    private static void AddTestRule(FakeCurrentUser user, InMemoryAlertRuleRepository rules, AlertType type = AlertType.AssetWarrantyExpiring)
    {
        rules.Add(new AlertRule(user.OrganizationId, type, DateTimeOffset.UtcNow, "admin@acme.test"));
    }

    [Fact]
    public async Task ListAlertRulesAsync_ForbiddenForNonPrivilegedRole()
    {
        var (service, user, _, _, _, _) = CreateService();
        user.Roles = ["employee"];

        var result = await service.ListAlertRulesAsync(CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("FORBIDDEN", result.Error!.Code);
    }

    [Fact]
    public async Task ListAlertRulesAsync_ReturnsAllTenTypes()
    {
        var (service, user, _, _, _, _) = CreateService();
        user.Roles = ["owner"];

        var result = await service.ListAlertRulesAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.Value!.Count);
    }

    [Fact]
    public async Task UpsertAlertRuleAsync_RejectsMoreThanFiveThresholds()
    {
        var (service, _, _, _, _, _) = CreateService();

        var request = new SaveAlertRuleRequest(
            IsEnabled: true,
            ThresholdDays: [10, 20, 30, 40, 50, 60],
            DeliveryMode: AlertDeliveryMode.Immediate,
            RecipientMode: AlertRecipientMode.OwnersAndAdmins,
            CustomEmails: null,
            CooldownDays: 1);

        var result = await service.UpsertAlertRuleAsync(AlertType.AssetWarrantyExpiring, request, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code);
    }

    [Fact]
    public async Task UpsertAlertRuleAsync_RejectsThresholdAbove365()
    {
        var (service, _, _, _, _, _) = CreateService();

        var request = new SaveAlertRuleRequest(
            IsEnabled: true,
            ThresholdDays: [400],
            DeliveryMode: AlertDeliveryMode.Immediate,
            RecipientMode: AlertRecipientMode.OwnersAndAdmins,
            CustomEmails: null,
            CooldownDays: 1);

        var result = await service.UpsertAlertRuleAsync(AlertType.AssetWarrantyExpiring, request, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code);
    }

    [Fact]
    public async Task UpsertAlertRuleAsync_RejectsThresholdBelow0()
    {
        var (service, _, _, _, _, _) = CreateService();

        var request = new SaveAlertRuleRequest(
            IsEnabled: true,
            ThresholdDays: [-1],
            DeliveryMode: AlertDeliveryMode.Immediate,
            RecipientMode: AlertRecipientMode.OwnersAndAdmins,
            CustomEmails: null,
            CooldownDays: 1);

        var result = await service.UpsertAlertRuleAsync(AlertType.AssetWarrantyExpiring, request, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code);
    }

    [Fact]
    public async Task UpsertAlertRuleAsync_RejectsInvalidCustomEmails()
    {
        var (service, _, _, _, _, _) = CreateService();

        var request = new SaveAlertRuleRequest(
            IsEnabled: true,
            ThresholdDays: [30],
            DeliveryMode: AlertDeliveryMode.Immediate,
            RecipientMode: AlertRecipientMode.Custom,
            CustomEmails: "not-an-email",
            CooldownDays: 1);

        var result = await service.UpsertAlertRuleAsync(AlertType.AssetWarrantyExpiring, request, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code);
    }

    [Fact]
    public async Task UpsertAlertRuleAsync_CreatesNewRule()
    {
        var (service, _, rules, _, _, _) = CreateService();

        var request = new SaveAlertRuleRequest(
            IsEnabled: true,
            ThresholdDays: [90, 30, 7],
            DeliveryMode: AlertDeliveryMode.Digest,
            RecipientMode: AlertRecipientMode.Custom,
            CustomEmails: "ops@acme.test,admin@acme.test",
            CooldownDays: 3);

        var result = await service.UpsertAlertRuleAsync(AlertType.LicenseExpiring, request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AlertType.LicenseExpiring, result.Value!.Type);
        Assert.Equal(new[] { 90, 30, 7 }, result.Value!.ThresholdDays);
        Assert.Equal(AlertDeliveryMode.Digest, result.Value!.DeliveryMode);
        Assert.Equal(AlertRecipientMode.Custom, result.Value!.RecipientMode);
        Assert.Equal("ops@acme.test,admin@acme.test", result.Value!.CustomEmails);
        Assert.Equal(3, result.Value!.CooldownDays);
        Assert.Single(rules.Rules);
    }

    [Fact]
    public async Task UpsertAlertRuleAsync_UpdatesExistingRule()
    {
        var (service, user, rules, _, _, _) = CreateService();
        AddTestRule(user, rules, AlertType.AssetWarrantyExpiring);

        var request = new SaveAlertRuleRequest(
            IsEnabled: false,
            ThresholdDays: [30, 7],
            DeliveryMode: AlertDeliveryMode.Immediate,
            RecipientMode: AlertRecipientMode.OwnersAndAdmins,
            CustomEmails: null,
            CooldownDays: 2);

        var result = await service.UpsertAlertRuleAsync(AlertType.AssetWarrantyExpiring, request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsEnabled);
        Assert.Equal(new[] { 30, 7 }, result.Value!.ThresholdDays);
        Assert.Equal(2, result.Value!.CooldownDays);
        Assert.Single(rules.Rules);
    }

    [Fact]
    public async Task GetAlertRuleAsync_ReturnsDefaultsForUnconfiguredType()
    {
        var (service, _, _, _, _, _) = CreateService();

        var result = await service.GetAlertRuleAsync(AlertType.LicenseExpiring, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AlertType.LicenseExpiring, result.Value!.Type);
        Assert.False(result.Value!.IsEnabled);
        Assert.Empty(result.Value!.ThresholdDays);
        Assert.Equal(AlertDeliveryMode.Immediate, result.Value!.DeliveryMode);
        Assert.Equal(AlertRecipientMode.OwnersAndAdmins, result.Value!.RecipientMode);
        Assert.Null(result.Value!.CustomEmails);
        Assert.Equal(1, result.Value!.CooldownDays);
    }

    [Fact]
    public async Task GetAlertDigestAsync_ReturnsDefaultsWhenNoneExists()
    {
        var (service, _, _, _, _, _) = CreateService();

        var result = await service.GetAlertDigestAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AlertDigestFrequency.Off, result.Value!.Frequency);
    }

    [Fact]
    public async Task UpsertAlertDigestAsync_RejectsWeeklyWithoutDayOfWeek()
    {
        var (service, _, _, _, _, _) = CreateService();

        var request = new SaveAlertDigestSettingsRequest(
            AlertDigestFrequency.Weekly,
            null,
            new TimeOnly(9, 0),
            null, null,
            AlertDigestBusinessDays.Weekdays,
            null,
            false);

        var result = await service.UpsertAlertDigestAsync(request, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("ALERT_DIGEST_WEEKDAY_REQUIRED", result.Error!.Code);
    }

    [Fact]
    public async Task UpsertAlertDigestAsync_CreatesNewSettings()
    {
        var (service, _, _, digest, _, _) = CreateService();

        var request = new SaveAlertDigestSettingsRequest(
            AlertDigestFrequency.Weekly,
            DayOfWeek.Monday,
            new TimeOnly(9, 30),
            new TimeOnly(22, 0),
            new TimeOnly(6, 0),
            AlertDigestBusinessDays.All,
            "PL",
            true);

        var result = await service.UpsertAlertDigestAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AlertDigestFrequency.Weekly, result.Value!.Frequency);
        Assert.Equal(DayOfWeek.Monday, result.Value!.DayOfWeek);
        Assert.Equal(new TimeOnly(9, 30), result.Value!.LocalTime);
        Assert.Equal(new TimeOnly(22, 0), result.Value!.QuietHoursStart);
        Assert.Equal(new TimeOnly(6, 0), result.Value!.QuietHoursEnd);
        Assert.Equal(AlertDigestBusinessDays.All, result.Value!.BusinessDays);
        Assert.Equal("PL", result.Value!.HolidayCalendarCountryCode);
        Assert.True(result.Value!.IncludeEmptyDigest);
        Assert.Single(digest.Settings);
    }

    [Fact]
    public async Task SendTestAlertAsync_SendsEmailToCurrentUser()
    {
        var (service, user, _, _, _, emailSender) = CreateService();

        var result = await service.SendTestAlertAsync(null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(emailSender.Sent);
        Assert.Equal(user.Email, emailSender.Sent[0].To);
        Assert.Contains("test alert", emailSender.Sent[0].Subject, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendTestAlertAsync_ForbiddenForNonPrivilegedRole()
    {
        var (service, user, _, _, _, _) = CreateService();
        user.Roles = ["employee"];

        var result = await service.SendTestAlertAsync(null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("FORBIDDEN", result.Error!.Code);
    }

    [Fact]
    public async Task ListSentAlertHistoryAsync_ForbiddenForNonPrivilegedRole()
    {
        var (service, user, _, _, _, _) = CreateService();
        user.Roles = ["technician"];

        var result = await service.ListSentAlertHistoryAsync(1, 25, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("FORBIDDEN", result.Error!.Code);
    }

    [Fact]
    public async Task ListSentAlertHistoryAsync_AllowedForAuditor()
    {
        var (service, user, _, _, _, _) = CreateService();
        user.Roles = ["auditor"];

        var result = await service.ListSentAlertHistoryAsync(1, 25, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Items);
        Assert.Equal(0, result.Value!.Total);
    }

    [Fact]
    public async Task ListSentAlertHistoryAsync_ReturnsPagedResults()
    {
        var (service, user, _, _, sentAlerts, _) = CreateService();

        for (var i = 0; i < 5; i++)
        {
            sentAlerts.Add(new SentAlert(user.OrganizationId, "AssetWarrantyExpiring:30:2026-01-01", Guid.NewGuid(), "user@acme.test", DateTimeOffset.UtcNow.AddDays(-i)));
        }

        var result = await service.ListSentAlertHistoryAsync(1, 10, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value!.Total);
        Assert.Equal(5, result.Value!.Items.Count);
        Assert.Equal(1, result.Value!.Page);
        Assert.Equal(10, result.Value!.PageSize);
    }

    [Fact]
    public async Task ListSentAlertHistoryAsync_ParsesAlertTypeFromKey()
    {
        var (service, user, _, _, sentAlerts, _) = CreateService();
        sentAlerts.Add(new SentAlert(user.OrganizationId, "LicenseExpiring:30:2026-01-01", Guid.NewGuid(), "user@acme.test", DateTimeOffset.UtcNow));

        var result = await service.ListSentAlertHistoryAsync(1, 25, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AlertType.LicenseExpiring, result.Value!.Items[0].Type);
    }
}
