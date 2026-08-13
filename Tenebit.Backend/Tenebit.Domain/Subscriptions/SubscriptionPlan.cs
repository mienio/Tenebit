namespace Tenebit.Domain.Subscriptions;

public sealed class SubscriptionPlan
{
    // Real free-tier limit (not a temporary testing bump) — in line with competitors' free tiers
    // and with what the pricing page already advertises ("Up to 10 assets").
    public static readonly SubscriptionPlan Free = new("free", "Free", 10, 0m, "USD");
    public static readonly SubscriptionPlan Pro = new("pro", "Pro", 1000, 10m, "USD");

    public static readonly IReadOnlyList<SubscriptionPlan> All = [Free, Pro];

    private SubscriptionPlan(string key, string name, int assetLimit, decimal monthlyPrice, string currency)
    {
        Key = key;
        Name = name;
        AssetLimit = assetLimit;
        MonthlyPrice = monthlyPrice;
        Currency = currency;
    }

    public string Key { get; }
    public string Name { get; }
    public int AssetLimit { get; }
    public decimal MonthlyPrice { get; }
    public string Currency { get; }

    public static SubscriptionPlan? FromKey(string key) => All.FirstOrDefault(p => p.Key == key);
}
