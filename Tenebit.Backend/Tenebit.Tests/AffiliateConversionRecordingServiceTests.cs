using Tenebit.Application.Abstractions;
using Tenebit.Application.Affiliates;
using Tenebit.Application.Common;
using Tenebit.Domain.Affiliates;
using Tenebit.Domain.Identity;
using Tenebit.Domain.Organizations;
using Tenebit.Domain.Subscriptions;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class AffiliateConversionRecordingServiceTests
{
    private sealed record Fixture(
        AffiliateConversionRecordingService Service, InMemoryAffiliateCodeRepository Codes, InMemoryAffiliateRepository Affiliates,
        InMemoryAffiliateConversionRepository Conversions, InMemoryAffiliatePayoutPeriodRepository Periods,
        InMemoryAffiliateProgramSettingsRepository Settings, FakeClock Clock, FakePaymentGateway PaymentGateway,
        InMemorySubscriptionRepository Subscriptions, InMemoryOrganizationUserRepository OrganizationUsers);

    private static (Fixture Fixture, Affiliate Affiliate, AffiliateCode Code) CreateFixture()
    {
        var codes = new InMemoryAffiliateCodeRepository();
        var affiliates = new InMemoryAffiliateRepository();
        var conversions = new InMemoryAffiliateConversionRepository();
        var periods = new InMemoryAffiliatePayoutPeriodRepository();
        var settings = new InMemoryAffiliateProgramSettingsRepository();
        var clock = new FakeClock { UtcNow = new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero) };
        var paymentGateway = new FakePaymentGateway();
        var subscriptions = new InMemorySubscriptionRepository();
        var organizationUsers = new InMemoryOrganizationUserRepository();
        var service = new AffiliateConversionRecordingService(
            codes, affiliates, conversions, periods, settings, new FakeUnitOfWork(), clock, paymentGateway, subscriptions, organizationUsers);

        var affiliate = new Affiliate("damian@example.com", "hash", "Damian", "Kowalski", "PL", clock.UtcNow);
        affiliate.Approve(clock.UtcNow);
        affiliates.Add(affiliate);
        var code = new AffiliateCode(affiliate.Id, "DAMIAN20", null, clock.UtcNow);
        codes.Add(code);

        return (new Fixture(service, codes, affiliates, conversions, periods, settings, clock, paymentGateway, subscriptions, organizationUsers), affiliate, code);
    }

    [Fact]
    public async Task Recording_the_same_paddle_transaction_twice_creates_only_one_conversion()
    {
        var (fixture, _, code) = CreateFixture();

        await fixture.Service.RecordConversionAsync(code.Code, Guid.NewGuid(), Guid.NewGuid(), "txn_1", AffiliateConversionEventType.InitialSale, fixture.Clock.UtcNow, 100m, 90m, "EUR", null, CancellationToken.None);
        await fixture.Service.RecordConversionAsync(code.Code, Guid.NewGuid(), Guid.NewGuid(), "txn_1", AffiliateConversionEventType.InitialSale, fixture.Clock.UtcNow, 100m, 90m, "EUR", null, CancellationToken.None);

        Assert.Single(fixture.Conversions.Conversions);
    }

    [Fact]
    public async Task Unknown_code_is_ignored_without_error()
    {
        var (fixture, _, _) = CreateFixture();
        var result = await fixture.Service.RecordConversionAsync("DOES-NOT-EXIST", Guid.NewGuid(), Guid.NewGuid(), "txn_2", AffiliateConversionEventType.InitialSale, fixture.Clock.UtcNow, 100m, 90m, "EUR", null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(fixture.Conversions.Conversions);
    }

    [Fact]
    public async Task A_sale_with_the_affiliates_own_email_is_flagged_for_review_and_excluded_from_the_payout_period()
    {
        var (fixture, affiliate, code) = CreateFixture();

        await fixture.Service.RecordConversionAsync(code.Code, Guid.NewGuid(), Guid.NewGuid(), "txn_3", AffiliateConversionEventType.InitialSale, fixture.Clock.UtcNow, 100m, 90m, "EUR", affiliate.Email, CancellationToken.None);

        var conversion = Assert.Single(fixture.Conversions.Conversions);
        Assert.True(conversion.RequiresReview);
        Assert.Null(conversion.AffiliatePayoutPeriodId);
        Assert.Empty(fixture.Periods.Periods);
    }

    [Fact]
    public async Task A_normal_sale_opens_a_payout_period_for_the_current_calendar_month_and_adds_the_commission()
    {
        var (fixture, _, code) = CreateFixture();

        await fixture.Service.RecordConversionAsync(code.Code, Guid.NewGuid(), Guid.NewGuid(), "txn_4", AffiliateConversionEventType.InitialSale, fixture.Clock.UtcNow, 100m, 90m, "EUR", "someone-else@buyer.test", CancellationToken.None);

        var conversion = Assert.Single(fixture.Conversions.Conversions);
        Assert.False(conversion.RequiresReview);
        Assert.NotNull(conversion.AffiliatePayoutPeriodId);

        var period = Assert.Single(fixture.Periods.Periods);
        Assert.Equal(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), period.PeriodStart);
        Assert.Equal(conversion.CommissionAmount, period.TotalCommission);
    }

    [Fact]
    public async Task A_renewal_outside_the_configured_commission_window_is_recorded_but_not_paid()
    {
        var (fixture, affiliate, code) = CreateFixture();
        var organizationId = Guid.NewGuid();

        fixture.Settings.Settings.Update(20m, AffiliateCommissionBase.Net, 3, 10, 20, 5, null, false, null, null, false);

        // First sale six months ago establishes the window anchor.
        var firstSaleDate = fixture.Clock.UtcNow.AddMonths(-6);
        await fixture.Service.RecordConversionAsync(code.Code, organizationId, Guid.NewGuid(), "txn_first", AffiliateConversionEventType.InitialSale, firstSaleDate, 100m, 90m, "EUR", "buyer@other.test", CancellationToken.None);

        // A renewal happening now (6 months later, window is only 3 months) must not count.
        var result = await fixture.Service.RecordConversionAsync(code.Code, organizationId, Guid.NewGuid(), "txn_renewal", AffiliateConversionEventType.Renewal, fixture.Clock.UtcNow, 100m, 90m, "EUR", "buyer@other.test", CancellationToken.None);

        Assert.True(result.IsSuccess);
        var renewal = fixture.Conversions.Conversions.Single(c => c.PaddleTransactionId == "txn_renewal");
        Assert.False(renewal.IsWithinCommissionWindow);
        Assert.Null(renewal.AffiliatePayoutPeriodId);
    }

    private static void SeedOrganizationWithOwner(Fixture fixture, Guid organizationId, string ownerEmail)
    {
        var owner = new OrganizationUser(organizationId, ownerEmail, "Owner", true);
        owner.Update(ownerEmail, "Owner", true, [TenebitRoles.Owner]);
        fixture.OrganizationUsers.Users.Add(owner);
    }

    [Fact]
    public async Task HandleWebhookAsync_records_a_conversion_from_a_transaction_completed_event_with_affiliate_attribution()
    {
        var (fixture, _, code) = CreateFixture();
        var organizationId = Guid.NewGuid();
        var subscription = new OrganizationSubscription(organizationId, "starter");
        subscription.AttachPaddleCustomer("ctm_1");
        fixture.Subscriptions.Add(subscription);
        SeedOrganizationWithOwner(fixture, organizationId, "buyer@other.test");

        fixture.PaymentGateway.NextWebhookEvent = new PaymentWebhookEvent(
            "evt_1", "transaction.completed", "ctm_1", "sub_1", "", SubscriptionStatus.Unknown,
            fixture.Clock.UtcNow, fixture.Clock.UtcNow, fixture.Clock.UtcNow, null,
            "txn_paddle_1", code.Code, 100m, 90m, "EUR", false);

        var result = await fixture.Service.HandleWebhookAsync("{}", "sig", CancellationToken.None);

        Assert.True(result.IsSuccess);
        var conversion = Assert.Single(fixture.Conversions.Conversions);
        Assert.Equal("txn_paddle_1", conversion.PaddleTransactionId);
        Assert.Equal(organizationId, conversion.OrganizationId);
        Assert.Equal(AffiliateConversionEventType.InitialSale, conversion.EventType);
        Assert.False(conversion.RequiresReview);
        Assert.NotNull(conversion.AffiliatePayoutPeriodId);
    }

    [Fact]
    public async Task HandleWebhookAsync_ignores_a_transaction_with_no_affiliate_attribution()
    {
        var (fixture, _, _) = CreateFixture();
        var organizationId = Guid.NewGuid();
        var subscription = new OrganizationSubscription(organizationId, "starter");
        subscription.AttachPaddleCustomer("ctm_2");
        fixture.Subscriptions.Add(subscription);

        fixture.PaymentGateway.NextWebhookEvent = new PaymentWebhookEvent(
            "evt_2", "transaction.completed", "ctm_2", "sub_2", "", SubscriptionStatus.Unknown,
            fixture.Clock.UtcNow, fixture.Clock.UtcNow, fixture.Clock.UtcNow, null,
            "txn_paddle_2", null, 100m, 90m, "EUR", false);

        var result = await fixture.Service.HandleWebhookAsync("{}", "sig", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(fixture.Conversions.Conversions);
    }

    [Fact]
    public async Task HandleWebhookAsync_ignores_subscription_lifecycle_events()
    {
        var (fixture, _, code) = CreateFixture();
        fixture.PaymentGateway.NextWebhookEvent = new PaymentWebhookEvent(
            "evt_3", "subscription.created", "ctm_3", "sub_3", "starter", SubscriptionStatus.Active,
            fixture.Clock.UtcNow, fixture.Clock.UtcNow, fixture.Clock.UtcNow.AddMonths(1), null,
            AffiliateCode: code.Code);

        var result = await fixture.Service.HandleWebhookAsync("{}", "sig", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(fixture.Conversions.Conversions);
    }

    [Fact]
    public async Task HandleWebhookAsync_AdjustmentCreated_compensates_the_original_conversion_in_its_own_still_open_period()
    {
        var (fixture, _, code) = CreateFixture();
        await fixture.Service.RecordConversionAsync(
            code.Code, Guid.NewGuid(), Guid.NewGuid(), "txn_refunded", AffiliateConversionEventType.InitialSale,
            fixture.Clock.UtcNow, 100m, 90m, "EUR", "buyer@other.test", CancellationToken.None);
        var original = Assert.Single(fixture.Conversions.Conversions);
        var originalPeriod = Assert.Single(fixture.Periods.Periods);

        fixture.PaymentGateway.NextWebhookEvent = new PaymentWebhookEvent(
            "evt_refund_1", "adjustment.created", "", null, "", SubscriptionStatus.Unknown,
            fixture.Clock.UtcNow, fixture.Clock.UtcNow, fixture.Clock.UtcNow, null,
            TransactionId: "adj_1", GrossAmount: 100m, NetAmount: 90m, OriginalTransactionId: "txn_refunded");

        var result = await fixture.Service.HandleWebhookAsync("{}", "sig", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, fixture.Conversions.Conversions.Count);
        var compensation = fixture.Conversions.Conversions.Single(c => c.PaddleTransactionId == "adj_1");
        Assert.Equal(-original.CommissionAmount, compensation.CommissionAmount);
        Assert.Equal(originalPeriod.Id, compensation.AffiliatePayoutPeriodId);
        Assert.Equal(0m, originalPeriod.TotalCommission);
    }

    [Fact]
    public async Task HandleWebhookAsync_AdjustmentCreated_nets_out_against_the_current_period_instead_of_rewriting_an_already_paid_one()
    {
        var (fixture, _, code) = CreateFixture();
        var saleDate = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        await fixture.Service.RecordConversionAsync(
            code.Code, Guid.NewGuid(), Guid.NewGuid(), "txn_refunded", AffiliateConversionEventType.InitialSale,
            saleDate, 100m, 90m, "EUR", "buyer@other.test", CancellationToken.None);
        var original = Assert.Single(fixture.Conversions.Conversions);
        var julyPeriod = Assert.Single(fixture.Periods.Periods);
        julyPeriod.Close(fixture.Clock.UtcNow);
        julyPeriod.MarkPaid();

        fixture.PaymentGateway.NextWebhookEvent = new PaymentWebhookEvent(
            "evt_refund_2", "adjustment.created", "", null, "", SubscriptionStatus.Unknown,
            fixture.Clock.UtcNow, fixture.Clock.UtcNow, fixture.Clock.UtcNow, null,
            TransactionId: "adj_2", GrossAmount: 100m, NetAmount: 90m, OriginalTransactionId: "txn_refunded");

        var result = await fixture.Service.HandleWebhookAsync("{}", "sig", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(9m, original.CommissionAmount); // sanity: 90 * 10%
        Assert.Equal(9m, julyPeriod.TotalCommission); // untouched - already paid
        var compensation = fixture.Conversions.Conversions.Single(c => c.PaddleTransactionId == "adj_2");
        var septemberPeriod = fixture.Periods.Periods.Single(p => p.Id != julyPeriod.Id);
        Assert.Equal(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), septemberPeriod.PeriodStart);
        Assert.Equal(-9m, septemberPeriod.TotalCommission);
        Assert.Equal(septemberPeriod.Id, compensation.AffiliatePayoutPeriodId);
    }

    [Fact]
    public async Task HandleWebhookAsync_AdjustmentCreated_is_idempotent_on_replay()
    {
        var (fixture, _, code) = CreateFixture();
        await fixture.Service.RecordConversionAsync(
            code.Code, Guid.NewGuid(), Guid.NewGuid(), "txn_refunded", AffiliateConversionEventType.InitialSale,
            fixture.Clock.UtcNow, 100m, 90m, "EUR", "buyer@other.test", CancellationToken.None);

        fixture.PaymentGateway.NextWebhookEvent = new PaymentWebhookEvent(
            "evt_refund_3", "adjustment.created", "", null, "", SubscriptionStatus.Unknown,
            fixture.Clock.UtcNow, fixture.Clock.UtcNow, fixture.Clock.UtcNow, null,
            TransactionId: "adj_3", GrossAmount: 100m, NetAmount: 90m, OriginalTransactionId: "txn_refunded");

        await fixture.Service.HandleWebhookAsync("{}", "sig", CancellationToken.None);
        await fixture.Service.HandleWebhookAsync("{}", "sig", CancellationToken.None);

        Assert.Equal(2, fixture.Conversions.Conversions.Count); // original + exactly one compensation
    }

    [Fact]
    public async Task HandleWebhookAsync_AdjustmentCreated_ignores_a_refund_for_a_transaction_that_was_never_an_affiliate_sale()
    {
        var (fixture, _, _) = CreateFixture();
        fixture.PaymentGateway.NextWebhookEvent = new PaymentWebhookEvent(
            "evt_refund_4", "adjustment.created", "", null, "", SubscriptionStatus.Unknown,
            fixture.Clock.UtcNow, fixture.Clock.UtcNow, fixture.Clock.UtcNow, null,
            TransactionId: "adj_4", GrossAmount: 100m, NetAmount: 90m, OriginalTransactionId: "txn_never_existed");

        var result = await fixture.Service.HandleWebhookAsync("{}", "sig", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(fixture.Conversions.Conversions);
    }
}
