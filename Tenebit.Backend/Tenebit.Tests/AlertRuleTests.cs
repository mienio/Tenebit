using Tenebit.Domain.Alerts;
using Tenebit.Domain.Common;

namespace Tenebit.Tests;

public class AlertRuleTests
{
    [Fact]
    public void Constructor_SetsSensibleDefaults()
    {
        var orgId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var rule = new AlertRule(orgId, AlertType.AssetWarrantyExpiring, now, "admin@acme.test");

        Assert.Equal(orgId, rule.OrganizationId);
        Assert.Equal(AlertType.AssetWarrantyExpiring, rule.Type);
        Assert.True(rule.IsEnabled);
        Assert.Empty(rule.ThresholdDays);
        Assert.Equal(AlertDeliveryMode.Immediate, rule.DeliveryMode);
        Assert.Equal(AlertRecipientMode.OwnersAndAdmins, rule.RecipientMode);
        Assert.Null(rule.CustomEmails);
        Assert.Equal(1, rule.CooldownDays);
        Assert.Equal(now, rule.CreatedAt);
        Assert.Equal(now, rule.UpdatedAt);
        Assert.Equal("admin@acme.test", rule.UpdatedBy);
    }

    [Fact]
    public void Constructor_ThrowsForEmptyOrganizationId()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<DomainException>(() => new AlertRule(Guid.Empty, AlertType.LicenseExpiring, now, "admin@acme.test"));
    }

    [Fact]
    public void Constructor_ThrowsForEmptyCreatedBy()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<DomainException>(() => new AlertRule(Guid.NewGuid(), AlertType.LicenseExpiring, now, ""));
    }

    [Fact]
    public void UpdateSettings_AppliesAllFields()
    {
        var rule = new AlertRule(Guid.NewGuid(), AlertType.AssetWarrantyExpiring, DateTimeOffset.UtcNow, "admin");

        var updated = DateTimeOffset.UtcNow.AddMinutes(1);
        rule.UpdateSettings(
            isEnabled: false,
            thresholdDays: new List<int> { 90, 30, 7 },
            deliveryMode: AlertDeliveryMode.Digest,
            recipientMode: AlertRecipientMode.Custom,
            customEmails: "ops@acme.test",
            cooldownDays: 3,
            updatedAt: updated,
            updatedBy: "owner@acme.test");

        Assert.False(rule.IsEnabled);
        Assert.Equal(new[] { 90, 30, 7 }, rule.ThresholdDays);
        Assert.Equal(AlertDeliveryMode.Digest, rule.DeliveryMode);
        Assert.Equal(AlertRecipientMode.Custom, rule.RecipientMode);
        Assert.Equal("ops@acme.test", rule.CustomEmails);
        Assert.Equal(3, rule.CooldownDays);
        Assert.Equal(updated, rule.UpdatedAt);
        Assert.Equal("owner@acme.test", rule.UpdatedBy);
    }

    [Fact]
    public void Enable_ShouldEnableRule()
    {
        var rule = new AlertRule(Guid.NewGuid(), AlertType.AssetWarrantyExpiring, DateTimeOffset.UtcNow, "admin");
        rule.Disable(DateTimeOffset.UtcNow, "admin");
        Assert.False(rule.IsEnabled);

        rule.Enable(DateTimeOffset.UtcNow, "admin");
        Assert.True(rule.IsEnabled);
    }

    [Fact]
    public void Disable_ShouldDisableRule()
    {
        var rule = new AlertRule(Guid.NewGuid(), AlertType.AssetWarrantyExpiring, DateTimeOffset.UtcNow, "admin");
        rule.Disable(DateTimeOffset.UtcNow, "owner");

        Assert.False(rule.IsEnabled);
    }
}

public class AlertDigestSettingsTests
{
    [Fact]
    public void Constructor_SetsSensibleDefaults()
    {
        var orgId = Guid.NewGuid();
        var settings = new AlertDigestSettings(orgId);

        Assert.Equal(orgId, settings.OrganizationId);
        Assert.Equal(AlertDigestFrequency.Off, settings.Frequency);
        Assert.Null(settings.DayOfWeek);
        Assert.Equal(new TimeOnly(8, 0), settings.LocalTime);
        Assert.Null(settings.QuietHoursStart);
        Assert.Null(settings.QuietHoursEnd);
        Assert.Equal(AlertDigestBusinessDays.Weekdays, settings.BusinessDays);
        Assert.Null(settings.HolidayCalendarCountryCode);
        Assert.False(settings.IncludeEmptyDigest);
        Assert.Null(settings.LastGeneratedAt);
    }

    [Fact]
    public void Constructor_ThrowsForEmptyOrganizationId()
    {
        Assert.Throws<DomainException>(() => new AlertDigestSettings(Guid.Empty));
    }

    [Fact]
    public void Update_AppliesAllFields()
    {
        var settings = new AlertDigestSettings(Guid.NewGuid());

        settings.Update(
            frequency: AlertDigestFrequency.Weekly,
            dayOfWeek: DayOfWeek.Monday,
            localTime: new TimeOnly(9, 30),
            quietHoursStart: new TimeOnly(22, 0),
            quietHoursEnd: new TimeOnly(6, 0),
            businessDays: AlertDigestBusinessDays.All,
            holidayCalendarCountryCode: "PL",
            includeEmptyDigest: true,
            lastGeneratedAt: DateTimeOffset.UtcNow);

        Assert.Equal(AlertDigestFrequency.Weekly, settings.Frequency);
        Assert.Equal(DayOfWeek.Monday, settings.DayOfWeek);
        Assert.Equal(new TimeOnly(9, 30), settings.LocalTime);
        Assert.Equal(new TimeOnly(22, 0), settings.QuietHoursStart);
        Assert.Equal(new TimeOnly(6, 0), settings.QuietHoursEnd);
        Assert.Equal(AlertDigestBusinessDays.All, settings.BusinessDays);
        Assert.Equal("PL", settings.HolidayCalendarCountryCode);
        Assert.True(settings.IncludeEmptyDigest);
        Assert.NotNull(settings.LastGeneratedAt);
    }
}
