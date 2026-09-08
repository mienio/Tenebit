namespace Tenebit.Domain.Subscriptions;

/// <summary>How often a paid plan is billed. Orthogonal to <see cref="SubscriptionPlan"/> (the tier) -
/// Paddle has a separate Price ID per (plan, interval) pair, see <c>Paddle:Prices:&lt;plan&gt;:monthly</c>/
/// <c>:annual</c> in configuration.</summary>
public enum BillingInterval
{
    Monthly,
    Annual
}
