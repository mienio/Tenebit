using Tenebit.Domain.Assets;
using Tenebit.Domain.Common;

namespace Tenebit.Tests;

/// <summary>
/// These deadlines are compliance obligations, so the date arithmetic is pinned rather than trusted.
/// The important case is the one people get wrong by hand: an inspection done early or late must shift
/// the whole cycle, otherwise the schedule silently drifts back to the original month.
/// </summary>
public class MaintenanceScheduleTests
{
    private static MaintenanceSchedule Create(int intervalMonths = 12, string name = "Przegląd") =>
        new(Guid.NewGuid(), Guid.NewGuid(), name, intervalMonths, new DateOnly(2026, 6, 1), DateTimeOffset.UtcNow);

    [Fact]
    public void Performing_early_moves_the_next_date_from_the_actual_date()
    {
        var schedule = Create();

        schedule.MarkPerformed(new DateOnly(2026, 5, 20), "Anna");

        // Counted from 20 May, not from the original 1 June deadline.
        Assert.Equal(new DateOnly(2027, 5, 20), schedule.NextDueOn);
        Assert.Equal(new DateOnly(2026, 5, 20), schedule.LastPerformedOn);
    }

    [Fact]
    public void Performing_late_also_counts_from_the_actual_date()
    {
        var schedule = Create();

        schedule.MarkPerformed(new DateOnly(2026, 7, 10), null);

        Assert.Equal(new DateOnly(2027, 7, 10), schedule.NextDueOn);
    }

    [Theory]
    [InlineData(6)]
    [InlineData(24)]
    public void Interval_is_respected(int months)
    {
        var schedule = Create(months);

        schedule.MarkPerformed(new DateOnly(2026, 1, 15), null);

        Assert.Equal(new DateOnly(2026, 1, 15).AddMonths(months), schedule.NextDueOn);
    }

    [Fact]
    public void Days_remaining_goes_negative_once_overdue()
    {
        var schedule = Create();

        Assert.Equal(10, schedule.DaysRemaining(new DateOnly(2026, 5, 22)));
        Assert.Equal(0, schedule.DaysRemaining(new DateOnly(2026, 6, 1)));
        Assert.Equal(-5, schedule.DaysRemaining(new DateOnly(2026, 6, 6)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(121)]
    public void Interval_outside_the_allowed_range_is_rejected(int months) =>
        Assert.Throws<DomainException>(() => Create(months));

    [Fact]
    public void Name_is_required() =>
        Assert.Throws<DomainException>(() => Create(12, "   "));
}
