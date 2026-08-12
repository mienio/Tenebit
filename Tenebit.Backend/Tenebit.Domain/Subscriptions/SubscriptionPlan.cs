namespace Tenebit.Domain.Subscriptions;

public sealed class SubscriptionPlan
{
    // Asset limit temporarily raised well above any realistic test usage while the product is in
    // free testing — no paywall should block testers. Lower this back down (e.g. to 10) once
    // paid plans go live for real.
    public static readonly SubscriptionPlan Free = new("free", "Free", 100_000, 0m, "USD");
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
