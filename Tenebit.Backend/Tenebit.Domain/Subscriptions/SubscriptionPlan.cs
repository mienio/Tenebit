namespace Tenebit.Domain.Subscriptions;

public sealed class SubscriptionPlan
{
    // Real free-tier limit (not a temporary testing bump) - in line with competitors' free tiers
    // and with what the pricing page already advertises ("Up to 10 assets").
    public static readonly SubscriptionPlan Free = new("free", "Free", 10, 0m, "EUR", "10");
    public static readonly SubscriptionPlan Starter = new("starter", "Starter", 100, 12m, "EUR", "100");
    public static readonly SubscriptionPlan Growth = new("growth", "Growth", 300, 29m, "EUR", "300");
    public static readonly SubscriptionPlan Business = new("business", "Business", 1000, 59m, "EUR", "1000");

    // Marketed as "1000+" / custom - the real enforcement ceiling is set well above what's advertised
    // as fair-use headroom against runaway scripts/bots, and is intentionally not surfaced in the API
    // response or UI copy.
    public static readonly SubscriptionPlan ThousandPlus = new("enterprise", "1000+", 10_000, 99m, "EUR", "1000+");

    public static readonly IReadOnlyList<SubscriptionPlan> All = [Free, Starter, Growth, Business, ThousandPlus];

    private SubscriptionPlan(string key, string name, int assetLimit, decimal monthlyPrice, string currency, string displayLimitLabel)
    {
        Key = key;
        Name = name;
        AssetLimit = assetLimit;
        MonthlyPrice = monthlyPrice;
        Currency = currency;
        DisplayLimitLabel = displayLimitLabel;
    }

    public string Key { get; }
    public string Name { get; }
    public int AssetLimit { get; }
    public decimal MonthlyPrice { get; }
    public string Currency { get; }

    /// <summary>Marketing label for the limit (e.g. "1000+" for the top tier) - may differ from the real
    /// enforced <see cref="AssetLimit"/>.</summary>
    public string DisplayLimitLabel { get; }

    // "pro" was the single legacy paid plan (1000 assets / $10) before the tiered lineup below existed.
    // Any subscription record still carrying that key resolves to the closest current equivalent so
    // existing Stripe subscriptions keep working without a data migration.
    private const string LegacyProKey = "pro";

    public static SubscriptionPlan? FromKey(string key) =>
        key == LegacyProKey ? Business : All.FirstOrDefault(p => p.Key == key);
}
