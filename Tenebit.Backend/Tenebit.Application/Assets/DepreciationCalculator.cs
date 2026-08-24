namespace Tenebit.Application.Assets;

public sealed record AssetBookValue(
    decimal PurchasePrice,
    decimal CurrentValue,
    decimal DepreciatedAmount,
    int? MonthsElapsed,
    int? DepreciationMonths,
    bool IsFullyDepreciated);

/// <summary>
/// Straight-line depreciation: an asset loses an equal share of its purchase price every month over the
/// category's schedule, down to zero. This is the method Polish accounting uses by default for the kinds
/// of equipment Tenebit tracks, and the one a finance team can reconcile against their own books.
///
/// Deliberately a pure function of (price, purchase date, schedule, today) with no repository access, so
/// the rule can be unit-tested directly and reused anywhere a value is displayed.
/// </summary>
public static class DepreciationCalculator
{
    /// <summary>
    /// Returns null when the asset simply has no book value to report - no price recorded, or the
    /// category is not depreciated at all. Callers then fall back to showing the purchase price.
    /// </summary>
    public static AssetBookValue? Calculate(decimal? purchasePrice, DateOnly? purchaseDate, int? depreciationMonths, DateOnly today)
    {
        if (purchasePrice is not { } price || price <= 0) return null;

        // No schedule, or no purchase date to measure from: the asset holds its full value.
        if (depreciationMonths is not { } months || months <= 0 || purchaseDate is not { } start)
        {
            return new AssetBookValue(price, price, 0m, null, depreciationMonths, false);
        }

        var elapsed = MonthsBetween(start, today);
        if (elapsed <= 0)
        {
            // Bought today, or a future-dated purchase - nothing has depreciated yet.
            return new AssetBookValue(price, price, 0m, 0, months, false);
        }

        if (elapsed >= months)
        {
            return new AssetBookValue(price, 0m, price, elapsed, months, true);
        }

        // Round to whole cents so displayed values sum to the same total the ledger would show.
        var remaining = Math.Round(price * (months - elapsed) / months, 2, MidpointRounding.AwayFromZero);
        return new AssetBookValue(price, remaining, price - remaining, elapsed, months, false);
    }

    /// <summary>Whole months between two dates; a partial month does not count until the day-of-month is reached.</summary>
    private static int MonthsBetween(DateOnly from, DateOnly to)
    {
        var months = ((to.Year - from.Year) * 12) + to.Month - from.Month;
        if (to.Day < from.Day) months--;
        return months;
    }
}
