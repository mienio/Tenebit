using Tenebit.Application.Affiliates;
using Tenebit.Domain.Affiliates;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class AffiliateConversionRecordingServiceTests
{
    private sealed record Fixture(
        AffiliateConversionRecordingService Service, InMemoryAffiliateCodeRepository Codes, InMemoryAffiliateRepository Affiliates,
        InMemoryAffiliateConversionRepository Conversions, InMemoryAffiliatePayoutPeriodRepository Periods,
        InMemoryAffiliateProgramSettingsRepository Settings, FakeClock Clock);

    private static (Fixture Fixture, Affiliate Affiliate, AffiliateCode Code) CreateFixture()
    {
        var codes = new InMemoryAffiliateCodeRepository();
        var affiliates = new InMemoryAffiliateRepository();
        var conversions = new InMemoryAffiliateConversionRepository();
        var periods = new InMemoryAffiliatePayoutPeriodRepository();
        var settings = new InMemoryAffiliateProgramSettingsRepository();
        var clock = new FakeClock { UtcNow = new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero) };
        var service = new AffiliateConversionRecordingService(codes, affiliates, conversions, periods, settings, new FakeUnitOfWork(), clock);

        var affiliate = new Affiliate("damian@example.com", "hash", "Damian", "Kowalski", "PL", clock.UtcNow);
        affiliate.Approve(clock.UtcNow);
        affiliates.Add(affiliate);
        var code = new AffiliateCode(affiliate.Id, "DAMIAN20", null, clock.UtcNow);
        codes.Add(code);

        return (new Fixture(service, codes, affiliates, conversions, periods, settings, clock), affiliate, code);
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

        fixture.Settings.Settings.Update(20m, AffiliateCommissionBase.Net, 3, 10, 20, 5, null, false, false);

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
}
