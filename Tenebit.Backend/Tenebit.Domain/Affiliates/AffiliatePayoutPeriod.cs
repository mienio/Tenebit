using Tenebit.Domain.Common;

namespace Tenebit.Domain.Affiliates;

public enum PayoutPeriodStatus
{
    /// <summary>The current calendar month - still accumulating conversions.</summary>
    Open,

    /// <summary>Calendar month is over; total is frozen and waiting for the admin to pay it (on or
    /// around the settings-configured payout day, see AffiliateProgramSettings).</summary>
    AwaitingPayout,

    /// <summary>Fully covered by at least one AffiliatePayout.</summary>
    Paid
}

/// <summary>
/// One calendar month of commission for one affiliate. This is the entity behind the product owner's
/// explicit cutoff rule: a sale on 2026-09-10 belongs to the "September" period
/// (<see cref="PeriodStart"/> = 2026-09-01, <see cref="PeriodEnd"/> = 2026-10-01 exclusive), which
/// only closes on 2026-10-01 and is only ever due around the 20th of <em>October</em> - never the
/// 20th of the same month the sale happened in. There is deliberately no "period so far, mid-month"
/// due date anywhere in this type; the payout day only ever applies to a period that has already
/// closed.
/// </summary>
public sealed class AffiliatePayoutPeriod
{
    private AffiliatePayoutPeriod() { }

    private AffiliatePayoutPeriod(Guid affiliateId, DateTimeOffset periodStart, DateTimeOffset periodEnd, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        AffiliateId = affiliateId;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        TotalCommission = 0m;
        Status = PayoutPeriodStatus.Open;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid AffiliateId { get; private set; }
    public DateTimeOffset PeriodStart { get; private set; }
    public DateTimeOffset PeriodEnd { get; private set; }
    public decimal TotalCommission { get; private set; }
    public PayoutPeriodStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    /// <summary>Resolves the calendar-month period a conversion at <paramref name="occurredAt"/>
    /// belongs to, in UTC - the single source of truth for the cutoff rule described on the type.</summary>
    public static (DateTimeOffset Start, DateTimeOffset End) ResolveMonthBounds(DateTimeOffset occurredAt)
    {
        var utc = occurredAt.ToUniversalTime();
        var start = new DateTimeOffset(utc.Year, utc.Month, 1, 0, 0, 0, TimeSpan.Zero);
        return (start, start.AddMonths(1));
    }

    public static AffiliatePayoutPeriod OpenFor(Guid affiliateId, DateTimeOffset occurredAt, DateTimeOffset now)
    {
        var (start, end) = ResolveMonthBounds(occurredAt);
        return new AffiliatePayoutPeriod(affiliateId, start, end, now);
    }

    public void AddCommission(decimal amount) => TotalCommission = Math.Round(TotalCommission + amount, 2);

    /// <summary>Only a period whose PeriodEnd has actually passed may close - guards against a job
    /// that runs early/late/twice from ever closing "this month" while sales can still land in it.</summary>
    public void Close(DateTimeOffset now)
    {
        if (Status != PayoutPeriodStatus.Open) throw new DomainException("Tylko otwarty okres można zamknąć.");
        if (now < PeriodEnd) throw new DomainException("Nie można zamknąć okresu przed jego zakończeniem.");
        Status = PayoutPeriodStatus.AwaitingPayout;
        ClosedAt = now;
    }

    public void MarkPaid()
    {
        if (Status != PayoutPeriodStatus.AwaitingPayout)
            throw new DomainException("Tylko okres oczekujący na wypłatę może zostać oznaczony jako opłacony.");
        Status = PayoutPeriodStatus.Paid;
    }
}
