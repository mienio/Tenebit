using Tenebit.Application.Alerts;
using Tenebit.Application.Common;
using Tenebit.Domain.Alerts;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Assignments;
using Tenebit.Domain.Audits;
using Tenebit.Domain.Identity;
using Tenebit.Domain.Licenses;
using Tenebit.Domain.Offboarding;
using Tenebit.Domain.Organizations;
using Tenebit.Domain.People;
using Tenebit.Domain.Procedures;
using Tenebit.Domain.Reservations;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class AlertCheckServiceTests
{
    private sealed record TestContext(
        AlertCheckService Service,
        Organization Organization,
        InMemoryAssetRepository Assets,
        InMemorySentAlertRepository SentAlerts,
        InMemoryAlertRuleRepository Rules,
        InMemoryAlertDigestSettingsRepository DigestSettings,
        InMemoryOffboardingCaseRepository Offboarding,
        InMemoryAssetAuditCampaignRepository Campaigns,
        InMemoryAssetAuditParticipantRepository Participants,
        InMemoryEquipmentReservationRepository Reservations,
        InMemoryLicenseRepository Licenses,
        InMemoryAssignmentRepository Assignments,
        InMemoryProcedureRepository Procedures,
        InMemoryPersonRepository People,
        FakeEmailSender EmailSender,
        FakeClock Clock);

    private static TestContext CreateService()
    {
        var organizations = new InMemoryOrganizationRepository();
        var organization = new Organization("Acme", "PL", "pl", "PLN", "UTC");
        organizations.Add(organization);

        var users = new InMemoryOrganizationUserRepository();
        var admin = new OrganizationUser(organization.Id, "admin@acme.test", "Admin", true);
        admin.Update(admin.Email, admin.DisplayName, true, [TenebitRoles.Owner]);
        users.Add(admin);

        var assets = new InMemoryAssetRepository();
        var assignments = new InMemoryAssignmentRepository();
        var procedures = new InMemoryProcedureRepository();
        var licenses = new InMemoryLicenseRepository();
        var people = new InMemoryPersonRepository();
        var sentAlerts = new InMemorySentAlertRepository();
        var rules = new InMemoryAlertRuleRepository();
        var digestSettings = new InMemoryAlertDigestSettingsRepository();
        var offboarding = new InMemoryOffboardingCaseRepository();
        var campaigns = new InMemoryAssetAuditCampaignRepository();
        var participants = new InMemoryAssetAuditParticipantRepository();
        var reservations = new InMemoryEquipmentReservationRepository();
        var emailSender = new FakeEmailSender();
        var clock = new FakeClock { UtcNow = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero) };

        var service = new AlertCheckService(
            organizations,
            users,
            assets,
            assignments,
            procedures,
            licenses,
            people,
            sentAlerts,
            rules,
            digestSettings,
            offboarding,
            campaigns,
            participants,
            reservations,
            emailSender,
            clock,
            new FakeUnitOfWork());

        return new TestContext(service, organization, assets, sentAlerts, rules, digestSettings, offboarding, campaigns, participants, reservations, licenses, assignments, procedures, people, emailSender, clock);
    }

    private static AlertRule CreateRule(Guid organizationId, AlertType type, int[] thresholds, AlertDeliveryMode deliveryMode = AlertDeliveryMode.Immediate, AlertRecipientMode recipientMode = AlertRecipientMode.OwnersAndAdmins, bool enabled = true, int cooldownDays = 1)
    {
        var rule = new AlertRule(organizationId, type, DateTimeOffset.UtcNow, "test");
        rule.UpdateSettings(enabled, thresholds.ToList(), deliveryMode, recipientMode, null, cooldownDays, "test", DateTimeOffset.UtcNow);
        return rule;
    }

    private static Asset AddWarrantyAsset(Organization organization, InMemoryAssetRepository assets, DateOnly warrantyUntil)
    {
        var asset = new Asset(organization.Id, Guid.NewGuid(), "Laptop", $"AT-{Guid.NewGuid():N}"[..8]);
        asset.UpdateCore(asset.Name, asset.AssetTag, null, null, null, null, null, null, null, null, warrantyUntil, null);
        assets.Add(asset);
        return asset;
    }

    private static Person AddPerson(TestContext ctx, string email = "jan@acme.test")
    {
        var person = new Person(ctx.Organization.Id, "Jan", "Kowalski", email);
        ctx.People.Add(person);
        return person;
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
        var ctx = CreateService();
        ctx.Rules.Add(CreateRule(ctx.Organization.Id, AlertType.AssetWarrantyExpiring, [30, 7]));
        AddWarrantyAsset(ctx.Organization, ctx.Assets, DateOnly.FromDateTime(ctx.Clock.UtcNow.UtcDateTime).AddDays(29));

        await ctx.Service.RunAsync(3, CancellationToken.None);

        Assert.Single(ctx.SentAlerts.Alerts);
        Assert.Equal(SentAlertStatus.Sent, ctx.SentAlerts.Alerts[0].Status);
        Assert.Single(ctx.EmailSender.Sent);

        await ctx.Service.RunAsync(3, CancellationToken.None);

        Assert.Single(ctx.EmailSender.Sent);
    }

    [Fact]
    public async Task RunAsync_DisabledRule_DoesNotSend()
    {
        var ctx = CreateService();
        ctx.Rules.Add(CreateRule(ctx.Organization.Id, AlertType.AssetWarrantyExpiring, [30, 7], enabled: false));
        AddWarrantyAsset(ctx.Organization, ctx.Assets, DateOnly.FromDateTime(ctx.Clock.UtcNow.UtcDateTime).AddDays(29));

        await ctx.Service.RunAsync(3, CancellationToken.None);

        Assert.Empty(ctx.EmailSender.Sent);
        Assert.Empty(ctx.SentAlerts.Alerts);
    }

    [Fact]
    public async Task RunAsync_ThresholdFromRule_ControlsDetection()
    {
        var ctx = CreateService();
        ctx.Rules.Add(CreateRule(ctx.Organization.Id, AlertType.AssetWarrantyExpiring, [7]));
        AddWarrantyAsset(ctx.Organization, ctx.Assets, DateOnly.FromDateTime(ctx.Clock.UtcNow.UtcDateTime).AddDays(29));

        await ctx.Service.RunAsync(3, CancellationToken.None);

        Assert.Empty(ctx.EmailSender.Sent);
    }

    [Fact]
    public async Task RunAsync_SmtpFailure_RecordsFailedWithoutMarkingSent()
    {
        var ctx = CreateService();
        ctx.Rules.Add(CreateRule(ctx.Organization.Id, AlertType.AssetWarrantyExpiring, [30, 7]));
        AddWarrantyAsset(ctx.Organization, ctx.Assets, DateOnly.FromDateTime(ctx.Clock.UtcNow.UtcDateTime).AddDays(29));
        ctx.EmailSender.FailFor.Add("admin@acme.test");

        await ctx.Service.RunAsync(3, CancellationToken.None);

        var record = Assert.Single(ctx.SentAlerts.Alerts);
        Assert.Equal(SentAlertStatus.Failed, record.Status);
        Assert.Null(record.SentAt);
        Assert.False(string.IsNullOrEmpty(record.LastError));
        Assert.Equal(1, record.AttemptCount);
        Assert.NotNull(record.NextAttemptAt);
        Assert.True(record.NextAttemptAt > ctx.Clock.UtcNow);
        Assert.NotEqual(SentAlertStatus.Sent, record.Status);
    }

    [Fact]
    public async Task RunAsync_RetryAfterFailure_SendsOnceEligible()
    {
        var ctx = CreateService();
        ctx.Rules.Add(CreateRule(ctx.Organization.Id, AlertType.AssetWarrantyExpiring, [30, 7]));
        AddWarrantyAsset(ctx.Organization, ctx.Assets, DateOnly.FromDateTime(ctx.Clock.UtcNow.UtcDateTime).AddDays(29));
        ctx.EmailSender.FailFor.Add("admin@acme.test");

        await ctx.Service.RunAsync(3, CancellationToken.None);

        ctx.Clock.UtcNow = ctx.Clock.UtcNow.AddHours(2);
        ctx.EmailSender.FailFor.Remove("admin@acme.test");

        await ctx.Service.RunAsync(3, CancellationToken.None);

        var record = Assert.Single(ctx.SentAlerts.Alerts);
        Assert.Equal(SentAlertStatus.Sent, record.Status);
        Assert.NotNull(record.SentAt);
        Assert.Equal(2, ctx.EmailSender.AttemptCount);
    }

    [Fact]
    public async Task RunAsync_AttemptCap_StopsRetryingAfterMaxAttempts()
    {
        var ctx = CreateService();
        ctx.Rules.Add(CreateRule(ctx.Organization.Id, AlertType.AssetWarrantyExpiring, [30, 7]));
        AddWarrantyAsset(ctx.Organization, ctx.Assets, DateOnly.FromDateTime(ctx.Clock.UtcNow.UtcDateTime).AddDays(29));
        ctx.EmailSender.FailFor.Add("admin@acme.test");

        for (var i = 0; i < 5; i++)
        {
            await ctx.Service.RunAsync(3, CancellationToken.None);
            ctx.Clock.UtcNow = ctx.Clock.UtcNow.AddHours(2);
        }

        var record = Assert.Single(ctx.SentAlerts.Alerts);
        Assert.Equal(SentAlertStatus.Failed, record.Status);
        Assert.Equal(5, record.AttemptCount);

        var attemptsSoFar = ctx.EmailSender.AttemptCount;
        Assert.Equal(5, attemptsSoFar);

        await ctx.Service.RunAsync(3, CancellationToken.None);

        Assert.Equal(SentAlertStatus.Failed, record.Status);
        Assert.Equal(5, record.AttemptCount);
        Assert.Equal(attemptsSoFar, ctx.EmailSender.AttemptCount);
    }

    [Fact]
    public async Task RunAsync_QuietHours_DefersSendUntilWindowEnds()
    {
        var ctx = CreateService();
        ctx.Rules.Add(CreateRule(ctx.Organization.Id, AlertType.AssetWarrantyExpiring, [30, 7]));
        AddWarrantyAsset(ctx.Organization, ctx.Assets, DateOnly.FromDateTime(ctx.Clock.UtcNow.UtcDateTime).AddDays(29));
        ctx.Organization.SetQuietHours(new TimeOnly(11, 0), new TimeOnly(13, 0));

        await ctx.Service.RunAsync(3, CancellationToken.None);

        Assert.Empty(ctx.EmailSender.Sent);
        Assert.DoesNotContain(ctx.SentAlerts.Alerts, x => x.Status == SentAlertStatus.Failed);

        ctx.Clock.UtcNow = ctx.Clock.UtcNow.AddHours(3);

        await ctx.Service.RunAsync(3, CancellationToken.None);

        Assert.Single(ctx.EmailSender.Sent);
        var record = Assert.Single(ctx.SentAlerts.Alerts);
        Assert.Equal(SentAlertStatus.Sent, record.Status);
    }

    // Service-level: new detections

    [Fact]
    public async Task RunAsync_OffboardingReturnDue_SendsAlert()
    {
        var ctx = CreateService();
        ctx.Rules.Add(CreateRule(ctx.Organization.Id, AlertType.OffboardingReturnDue, [7]));

        var person = AddPerson(ctx);
        var offboardingCase = new OffboardingCase(ctx.Organization.Id, person.Id, ctx.Clock.UtcNow.AddDays(5), ctx.Clock.UtcNow.AddDays(3), null, null, null, false, false, false, "test", ctx.Clock.UtcNow);
        offboardingCase.Start(ctx.Clock.UtcNow);
        ctx.Offboarding.Add(offboardingCase);

        await ctx.Service.RunAsync(3, CancellationToken.None);

        Assert.Contains(ctx.EmailSender.Sent, e => e.To == "admin@acme.test");
        Assert.Contains(ctx.EmailSender.Sent, e => e.To == "jan@acme.test");
        var record = ctx.SentAlerts.Alerts.First();
        Assert.Equal(SentAlertStatus.Sent, record.Status);
        Assert.StartsWith("OffboardingReturnDue:", record.AlertKey);
    }

    [Fact]
    public async Task RunAsync_AssetAuditNoResponse_SendsAlert()
    {
        var ctx = CreateService();
        ctx.Rules.Add(CreateRule(ctx.Organization.Id, AlertType.AssetAuditNoResponse, [7]));

        var person = AddPerson(ctx);
        var campaign = new AssetAuditCampaign(ctx.Organization.Id, "Kampania Q1", null, ctx.Clock.UtcNow.AddDays(3), null, "test", ctx.Clock.UtcNow);
        campaign.Start(ctx.Clock.UtcNow);
        ctx.Campaigns.Add(campaign);

        var participant = new AssetAuditParticipant(ctx.Organization.Id, campaign.Id, person.Id, "jan@acme.test");
        ctx.Participants.Add(participant);

        await ctx.Service.RunAsync(3, CancellationToken.None);

        Assert.Contains(ctx.EmailSender.Sent, e => e.To == "admin@acme.test");
        Assert.StartsWith("AssetAuditNoResponse:", ctx.SentAlerts.Alerts.First().AlertKey);
    }

    [Fact]
    public async Task RunAsync_ReservationAwaitingApproval_SendsAlert()
    {
        var ctx = CreateService();
        ctx.Rules.Add(CreateRule(ctx.Organization.Id, AlertType.ReservationAwaitingApproval, [3]));

        var person = AddPerson(ctx);
        var requestedAt = ctx.Clock.UtcNow.AddDays(-5);
        var reservation = new EquipmentReservation(ctx.Organization.Id, person.Id, requestedAt.AddDays(1), requestedAt.AddDays(3), "Cel", null, null);
        reservation.AddItem(Guid.NewGuid(), 1, null);
        reservation.Submit(requestedAt);
        ctx.Reservations.Add(reservation);

        await ctx.Service.RunAsync(3, CancellationToken.None);

        Assert.Contains(ctx.EmailSender.Sent, e => e.To == "admin@acme.test");
        Assert.StartsWith("ReservationAwaitingApproval:", ctx.SentAlerts.Alerts.First().AlertKey);
    }

    [Fact]
    public async Task RunAsync_ReservationPickupUpcoming_SendsAlert()
    {
        var ctx = CreateService();
        ctx.Rules.Add(CreateRule(ctx.Organization.Id, AlertType.ReservationPickupUpcoming, [7]));

        var person = AddPerson(ctx);
        var startAt = ctx.Clock.UtcNow.AddDays(2);
        var reservation = new EquipmentReservation(ctx.Organization.Id, person.Id, startAt, startAt.AddDays(3), "Cel", null, null);
        reservation.AddItem(Guid.NewGuid(), 1, null);
        reservation.Submit(ctx.Clock.UtcNow.AddDays(-10));
        reservation.Approve(ctx.Clock.UtcNow.AddDays(-5), "admin");
        ctx.Reservations.Add(reservation);

        await ctx.Service.RunAsync(3, CancellationToken.None);

        Assert.Contains(ctx.EmailSender.Sent, e => e.To == "admin@acme.test");
        Assert.StartsWith("ReservationPickupUpcoming:", ctx.SentAlerts.Alerts.First().AlertKey);
    }

    [Fact]
    public async Task RunAsync_ReservationOverdue_SendsAlert()
    {
        var ctx = CreateService();
        ctx.Rules.Add(CreateRule(ctx.Organization.Id, AlertType.ReservationOverdue, [0]));

        var person = AddPerson(ctx);
        var endAt = ctx.Clock.UtcNow.AddDays(-2);
        var reservation = new EquipmentReservation(ctx.Organization.Id, person.Id, endAt.AddDays(-3), endAt, "Cel", null, null);
        var item = reservation.AddItem(Guid.NewGuid(), 1, null);
        reservation.Submit(endAt.AddDays(-10));
        reservation.Approve(endAt.AddDays(-9), "admin");
        item.Allocate(Guid.NewGuid());
        reservation.MarkCheckedOut(Guid.NewGuid(), endAt.AddDays(-8));
        ctx.Reservations.Add(reservation);

        await ctx.Service.RunAsync(3, CancellationToken.None);

        Assert.Contains(ctx.EmailSender.Sent, e => e.To == "admin@acme.test");
        Assert.StartsWith("ReservationOverdue:", ctx.SentAlerts.Alerts.First().AlertKey);
    }

    // Service-level: digest

    [Fact]
    public async Task RunAsync_DailyDigest_SendsSingleEmail()
    {
        var ctx = CreateService();
        ctx.Rules.Add(CreateRule(ctx.Organization.Id, AlertType.AssetWarrantyExpiring, [30], AlertDeliveryMode.Digest));

        var digest = new AlertDigestSettings(ctx.Organization.Id);
        digest.Update(AlertDigestFrequency.Daily, null, new TimeOnly(8, 0), null, null, AlertDigestBusinessDays.Weekdays, null, false, null);
        ctx.DigestSettings.Add(digest);

        AddWarrantyAsset(ctx.Organization, ctx.Assets, DateOnly.FromDateTime(ctx.Clock.UtcNow.UtcDateTime).AddDays(29));

        await ctx.Service.RunAsync(3, CancellationToken.None);

        // Digest mode: no immediate SentAlert, one aggregated email to the admin.
        Assert.Empty(ctx.SentAlerts.Alerts);
        var sent = Assert.Single(ctx.EmailSender.Sent);
        Assert.StartsWith("Tenebit —", sent.Subject);
        Assert.NotNull(digest.LastGeneratedAt);
    }

    [Fact]
    public async Task RunAsync_WeeklyDigest_SendsOnlyOnConfiguredDay()
    {
        var ctx = CreateService();
        ctx.Rules.Add(CreateRule(ctx.Organization.Id, AlertType.AssetWarrantyExpiring, [30], AlertDeliveryMode.Digest));

        // clock is Thursday 2026-01-01; a digest scheduled for Friday must NOT fire.
        var digest = new AlertDigestSettings(ctx.Organization.Id);
        digest.Update(AlertDigestFrequency.Weekly, DayOfWeek.Friday, new TimeOnly(8, 0), null, null, AlertDigestBusinessDays.Weekdays, null, false, null);
        ctx.DigestSettings.Add(digest);

        AddWarrantyAsset(ctx.Organization, ctx.Assets, DateOnly.FromDateTime(ctx.Clock.UtcNow.UtcDateTime).AddDays(29));

        await ctx.Service.RunAsync(3, CancellationToken.None);

        Assert.Empty(ctx.EmailSender.Sent);
        Assert.Null(digest.LastGeneratedAt);

        // Advance to Friday and it should fire.
        ctx.Clock.UtcNow = ctx.Clock.UtcNow.AddDays(1);
        await ctx.Service.RunAsync(3, CancellationToken.None);

        Assert.Single(ctx.EmailSender.Sent);
        Assert.NotNull(digest.LastGeneratedAt);
    }

    [Fact]
    public async Task RunAsync_EmptyDigest_NotSentWhenIncludeEmptyDigestFalse()
    {
        var ctx = CreateService();
        ctx.Rules.Add(CreateRule(ctx.Organization.Id, AlertType.AssetWarrantyExpiring, [30], AlertDeliveryMode.Digest));

        var digest = new AlertDigestSettings(ctx.Organization.Id);
        digest.Update(AlertDigestFrequency.Daily, null, new TimeOnly(8, 0), null, null, AlertDigestBusinessDays.Weekdays, null, false, null);
        ctx.DigestSettings.Add(digest);

        // No matching assets — the digest must not be sent.
        await ctx.Service.RunAsync(3, CancellationToken.None);

        Assert.Empty(ctx.EmailSender.Sent);
        Assert.NotNull(digest.LastGeneratedAt);
    }

    // ---------- Poprawka 1: domyślne reguły ----------

    [Fact]
    public void StarterAlertRules_Create_EnablesLegacyTypesAndDisablesNew()
    {
        var rules = StarterAlertRules.Create(Guid.NewGuid(), DateTimeOffset.UtcNow, "system");

        Assert.Equal(10, rules.Count);
        Assert.True(rules.Single(r => r.Type == AlertType.AssetWarrantyExpiring).IsEnabled);
        Assert.True(rules.Single(r => r.Type == AlertType.AssignmentReturnDue).IsEnabled);
        Assert.True(rules.Single(r => r.Type == AlertType.AssignmentNotConfirmed).IsEnabled);

        Assert.False(rules.Single(r => r.Type == AlertType.LicenseExpiring).IsEnabled);
        Assert.False(rules.Single(r => r.Type == AlertType.ProcedureReviewDue).IsEnabled);
        Assert.False(rules.Single(r => r.Type == AlertType.OffboardingReturnDue).IsEnabled);
        Assert.False(rules.Single(r => r.Type == AlertType.AssetAuditNoResponse).IsEnabled);
        Assert.False(rules.Single(r => r.Type == AlertType.ReservationAwaitingApproval).IsEnabled);
        Assert.False(rules.Single(r => r.Type == AlertType.ReservationPickupUpcoming).IsEnabled);
        Assert.False(rules.Single(r => r.Type == AlertType.ReservationOverdue).IsEnabled);

        Assert.Equal(new[] { 30, 7 }, rules.Single(r => r.Type == AlertType.AssetWarrantyExpiring).ThresholdDays);
    }

    // ---------- Poprawka 2: OwnersAndAdmins + odpowiedzialna osoba ----------

    [Fact]
    public async Task RunAsync_OwnersAndAdmins_AlsoNotifiesResponsiblePerson()
    {
        var ctx = CreateService();
        ctx.Rules.Add(CreateRule(ctx.Organization.Id, AlertType.AssignmentReturnDue, [0]));
        var person = AddPerson(ctx, "person@acme.test");
        ctx.Assignments.Add(new Assignment(ctx.Organization.Id, person.Id, "P-001",
            ctx.Clock.UtcNow.AddDays(-10), DateOnly.FromDateTime(ctx.Clock.UtcNow.UtcDateTime).AddDays(-2), null, "test"));

        await ctx.Service.RunAsync(3, CancellationToken.None);

        Assert.Contains(ctx.EmailSender.Sent, e => e.To == "admin@acme.test");
        Assert.Contains(ctx.EmailSender.Sent, e => e.To == "person@acme.test");
    }

    // ---------- Poprawka 3: brak duplikatów dla przeterminowanych ----------

    [Fact]
    public async Task RunAsync_OverdueAssignment_WithMultipleThresholds_SendsSingleAlert()
    {
        var ctx = CreateService();
        ctx.Rules.Add(CreateRule(ctx.Organization.Id, AlertType.AssignmentReturnDue, [7, 3, 1]));
        var person = AddPerson(ctx);
        ctx.Assignments.Add(new Assignment(ctx.Organization.Id, person.Id, "P-001",
            ctx.Clock.UtcNow.AddDays(-20), DateOnly.FromDateTime(ctx.Clock.UtcNow.UtcDateTime).AddDays(-10), null, "test"));

        await ctx.Service.RunAsync(3, CancellationToken.None);

        // przeterminowane o 10 dni → dokładnie jeden alert (dla 2 odbiorców), a nie 3 progi × 2 odbiorców
        Assert.Equal(2, ctx.EmailSender.Sent.Count);
        Assert.Equal(2, ctx.SentAlerts.Alerts.Count);
    }

    // ---------- Poprawka 4: MarkOverdue ----------

    [Fact]
    public async Task RunAsync_NotConfirmed_MarksAssignmentOverdue()
    {
        var ctx = CreateService();
        ctx.Rules.Add(CreateRule(ctx.Organization.Id, AlertType.AssignmentNotConfirmed, [0]));
        var person = AddPerson(ctx);
        var assignment = new Assignment(ctx.Organization.Id, person.Id, "P-001", ctx.Clock.UtcNow.AddDays(-10), null, null, "test");
        ctx.Assignments.Add(assignment);

        await ctx.Service.RunAsync(3, CancellationToken.None);

        Assert.Equal(AssignmentStatus.Overdue, assignment.Status);
    }

    // ---------- Poprawka 5: BusinessDays ----------

    [Fact]
    public async Task RunAsync_Digest_RespectsBusinessDays()
    {
        var ctx = CreateService();
        ctx.Rules.Add(CreateRule(ctx.Organization.Id, AlertType.AssetWarrantyExpiring, [30], AlertDeliveryMode.Digest));

        ctx.Clock.UtcNow = new DateTimeOffset(2026, 1, 3, 12, 0, 0, TimeSpan.Zero); // sobota
        var digest = new AlertDigestSettings(ctx.Organization.Id);
        digest.Update(AlertDigestFrequency.Daily, null, new TimeOnly(8, 0), null, null, AlertDigestBusinessDays.Weekdays, null, false, null);
        ctx.DigestSettings.Add(digest);
        AddWarrantyAsset(ctx.Organization, ctx.Assets, DateOnly.FromDateTime(ctx.Clock.UtcNow.UtcDateTime).AddDays(29));

        await ctx.Service.RunAsync(3, CancellationToken.None);
        Assert.Empty(ctx.EmailSender.Sent); // sobota nie jest dniem roboczym

        ctx.Clock.UtcNow = new DateTimeOffset(2026, 1, 5, 12, 0, 0, TimeSpan.Zero); // poniedziałek
        await ctx.Service.RunAsync(3, CancellationToken.None);
        Assert.Single(ctx.EmailSender.Sent);
    }

    // ---------- Poprawka 6: CooldownDays ----------

    [Fact]
    public async Task RunAsync_Cooldown_SuppressesSecondAlertForSameEntity()
    {
        var ctx = CreateService();
        ctx.Rules.Add(CreateRule(ctx.Organization.Id, AlertType.AssetWarrantyExpiring, [30, 7], cooldownDays: 5));
        AddWarrantyAsset(ctx.Organization, ctx.Assets, DateOnly.FromDateTime(ctx.Clock.UtcNow.UtcDateTime).AddDays(5));

        await ctx.Service.RunAsync(3, CancellationToken.None);

        // aktywo z gwarancją za 5 dni trafia w progi 30 i 7, ale cooldown tłumi drugi alert → 1 wysyłka
        Assert.Single(ctx.EmailSender.Sent);
        Assert.Single(ctx.SentAlerts.Alerts);
    }

    // ---------- Poprawka 7: LicenseExpiring / ProcedureReviewDue ----------

    [Fact]
    public async Task RunAsync_LicenseExpiring_SendsAlertWithoutRevealingKey()
    {
        var ctx = CreateService();
        ctx.Rules.Add(CreateRule(ctx.Organization.Id, AlertType.LicenseExpiring, [30]));
        ctx.Licenses.Add(new License(ctx.Organization.Id, "Adobe Creative", "Adobe", "SUPER-SECRET-KEY", 10,
            DateOnly.FromDateTime(ctx.Clock.UtcNow.UtcDateTime).AddDays(10), null));

        await ctx.Service.RunAsync(3, CancellationToken.None);

        var sent = Assert.Single(ctx.EmailSender.Sent);
        Assert.Equal("admin@acme.test", sent.To);
        Assert.DoesNotContain("SUPER-SECRET-KEY", ctx.EmailSender.Bodies.Single());
    }

    [Fact]
    public async Task RunAsync_ProcedureReviewDue_SendsAlert()
    {
        var ctx = CreateService();
        ctx.Rules.Add(CreateRule(ctx.Organization.Id, AlertType.ProcedureReviewDue, [30]));
        var procedure = new Procedure(ctx.Organization.Id, "Regulamin", "1.0", "HR", true);
        procedure.Update("Regulamin", "1.0", "HR", null, DateOnly.FromDateTime(ctx.Clock.UtcNow.UtcDateTime).AddDays(10), true);
        ctx.Procedures.Add(procedure);

        await ctx.Service.RunAsync(3, CancellationToken.None);

        Assert.Single(ctx.EmailSender.Sent);
    }
}
