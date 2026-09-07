using Tenebit.Application.Admin;
using Tenebit.Domain.Affiliates;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class AffiliateAdminServiceTests
{
    private sealed record Fixture(
        AffiliateAdminService Service, InMemoryAffiliateRepository Affiliates, InMemoryAffiliatePayoutPeriodRepository Periods,
        InMemoryAffiliatePayoutRepository Payouts, InMemoryAdminRepository Admin, FakeClock Clock);

    private static (Fixture Fixture, Affiliate Affiliate) CreateFixture()
    {
        var affiliates = new InMemoryAffiliateRepository();
        var periods = new InMemoryAffiliatePayoutPeriodRepository();
        var payouts = new InMemoryAffiliatePayoutRepository();
        var admin = new InMemoryAdminRepository();
        var clock = new FakeClock();
        var service = new AffiliateAdminService(
            affiliates, new InMemoryAffiliateCodeRepository(), new InMemoryAffiliateConversionRepository(),
            periods, payouts, new InMemoryAffiliateProgramSettingsRepository(), new InMemoryAffiliateMessageThreadRepository(),
            admin, new FakeUnitOfWork(), clock);

        var affiliate = new Affiliate("damian@example.com", "hash", "Damian", "Kowalski", "PL", clock.UtcNow);
        affiliates.Add(affiliate);

        return (new Fixture(service, affiliates, periods, payouts, admin, clock), affiliate);
    }

    [Fact]
    public async Task Approving_an_affiliate_writes_an_audit_entry()
    {
        var (fixture, affiliate) = CreateFixture();
        var result = await fixture.Service.ApproveAsync(affiliate.Id, "203.0.113.5", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AffiliateStatus.Active, fixture.Affiliates.Affiliates.Single().Status);
        Assert.Single(fixture.Admin.AuditEntries, e => e.Action == AdminActions.AffiliateApproved);
    }

    [Fact]
    public async Task Blocking_requires_a_reason_and_writes_an_audit_entry()
    {
        var (fixture, affiliate) = CreateFixture();
        var result = await fixture.Service.BlockAsync(affiliate.Id, "Spam links posted publicly", "203.0.113.5", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AffiliateStatus.Blocked, fixture.Affiliates.Affiliates.Single().Status);
        Assert.Single(fixture.Admin.AuditEntries, e => e.Action == AdminActions.AffiliateBlocked);
    }

    [Fact]
    public async Task Mark_paid_rejects_a_period_that_is_not_awaiting_payout()
    {
        var (fixture, affiliate) = CreateFixture();
        var openPeriod = AffiliatePayoutPeriod.OpenFor(affiliate.Id, fixture.Clock.UtcNow, fixture.Clock.UtcNow);
        fixture.Periods.Add(openPeriod);

        var result = await fixture.Service.MarkPayoutPaidAsync(affiliate.Id, [openPeriod.Id], 50m, "EUR", "REF123", null, "203.0.113.5", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(fixture.Payouts.Payouts);
    }

    [Fact]
    public async Task Mark_paid_with_a_valid_awaiting_period_creates_a_payout_and_closes_the_period()
    {
        var (fixture, affiliate) = CreateFixture();
        var period = AffiliatePayoutPeriod.OpenFor(affiliate.Id, fixture.Clock.UtcNow, fixture.Clock.UtcNow);
        period.AddCommission(42.50m);
        period.Close(fixture.Clock.UtcNow.AddMonths(2));
        fixture.Periods.Add(period);

        var result = await fixture.Service.MarkPayoutPaidAsync(affiliate.Id, [period.Id], 42.50m, "EUR", "REF123", "Wypłacone ręcznie", "203.0.113.5", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PayoutPeriodStatus.Paid, period.Status);
        var payout = Assert.Single(fixture.Payouts.Payouts);
        Assert.Equal(42.50m, payout.Amount);
        Assert.Single(fixture.Admin.AuditEntries, e => e.Action == AdminActions.AffiliatePayoutMarkedPaid);
    }

    [Fact]
    public async Task Closing_due_periods_only_closes_periods_whose_month_has_actually_ended()
    {
        var (fixture, affiliate) = CreateFixture();
        var septemberSale = new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.Zero);
        var period = AffiliatePayoutPeriod.OpenFor(affiliate.Id, septemberSale, septemberSale);
        fixture.Periods.Add(period);

        fixture.Clock.UtcNow = new DateTimeOffset(2026, 9, 25, 0, 0, 0, TimeSpan.Zero); // still September
        var closedTooEarly = await fixture.Service.CloseDuePeriodsAsync(CancellationToken.None);
        Assert.Equal(0, closedTooEarly);
        Assert.Equal(PayoutPeriodStatus.Open, period.Status);

        fixture.Clock.UtcNow = new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero); // October: month has ended
        var closedOnTime = await fixture.Service.CloseDuePeriodsAsync(CancellationToken.None);
        Assert.Equal(1, closedOnTime);
        Assert.Equal(PayoutPeriodStatus.AwaitingPayout, period.Status);
    }
}
