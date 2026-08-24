using Tenebit.Application.Assets;

namespace Tenebit.Tests;

/// <summary>
/// Book values feed a finance report, so the arithmetic is pinned here rather than left to be
/// eyeballed in the UI. The cases cover the boundaries a real fleet hits: the day of purchase, a
/// partial month, the final month, and everything past the end of the schedule.
/// </summary>
public class DepreciationCalculatorTests
{
    private static readonly DateOnly Bought = new(2024, 1, 15);

    [Fact]
    public void No_price_means_no_book_value()
    {
        Assert.Null(DepreciationCalculator.Calculate(null, Bought, 36, new DateOnly(2026, 1, 15)));
        Assert.Null(DepreciationCalculator.Calculate(0m, Bought, 36, new DateOnly(2026, 1, 15)));
    }

    [Fact]
    public void Category_without_schedule_keeps_full_value()
    {
        var value = DepreciationCalculator.Calculate(5000m, Bought, null, new DateOnly(2030, 1, 1));

        Assert.NotNull(value);
        Assert.Equal(5000m, value!.CurrentValue);
        Assert.Equal(0m, value.DepreciatedAmount);
        Assert.False(value.IsFullyDepreciated);
    }

    [Fact]
    public void Nothing_depreciates_on_the_day_of_purchase()
    {
        var value = DepreciationCalculator.Calculate(3600m, Bought, 36, Bought);

        Assert.Equal(3600m, value!.CurrentValue);
        Assert.Equal(0, value.MonthsElapsed);
    }

    [Fact]
    public void A_partial_month_does_not_count_until_the_day_is_reached()
    {
        // One day short of the first full month.
        var almost = DepreciationCalculator.Calculate(3600m, Bought, 36, new DateOnly(2024, 2, 14));
        Assert.Equal(0, almost!.MonthsElapsed);
        Assert.Equal(3600m, almost.CurrentValue);

        var exact = DepreciationCalculator.Calculate(3600m, Bought, 36, new DateOnly(2024, 2, 15));
        Assert.Equal(1, exact!.MonthsElapsed);
        Assert.Equal(3500m, exact.CurrentValue);
    }

    [Fact]
    public void Half_way_through_the_schedule_half_the_value_remains()
    {
        var value = DepreciationCalculator.Calculate(3600m, Bought, 36, new DateOnly(2025, 7, 15));

        Assert.Equal(18, value!.MonthsElapsed);
        Assert.Equal(1800m, value.CurrentValue);
        Assert.Equal(1800m, value.DepreciatedAmount);
    }

    [Fact]
    public void Past_the_schedule_the_value_is_zero_and_never_negative()
    {
        var value = DepreciationCalculator.Calculate(3600m, Bought, 36, new DateOnly(2035, 1, 1));

        Assert.Equal(0m, value!.CurrentValue);
        Assert.Equal(3600m, value.DepreciatedAmount);
        Assert.True(value.IsFullyDepreciated);
    }

    [Fact]
    public void A_future_dated_purchase_is_not_depreciated()
    {
        var value = DepreciationCalculator.Calculate(1000m, new DateOnly(2027, 6, 1), 24, new DateOnly(2026, 1, 1));

        Assert.Equal(1000m, value!.CurrentValue);
        Assert.Equal(0, value.MonthsElapsed);
    }

    [Fact]
    public void Current_and_depreciated_always_sum_to_the_purchase_price()
    {
        foreach (var month in Enumerable.Range(0, 40))
        {
            var value = DepreciationCalculator.Calculate(999.99m, Bought, 36, Bought.AddMonths(month));
            Assert.Equal(999.99m, value!.CurrentValue + value.DepreciatedAmount);
        }
    }
}
