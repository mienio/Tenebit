using Tenebit.Domain.Common;

namespace Tenebit.Domain.Subscriptions;

public sealed class OrganizationSubscription
{
    private OrganizationSubscription() { }

    public OrganizationSubscription(Guid organizationId, string planKey)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        PlanKey = planKey;
        Status = SubscriptionStatus.Active;
        CurrentPeriodStart = DateTimeOffset.UtcNow;
        CurrentPeriodEnd = CurrentPeriodStart.AddMonths(1);
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string PlanKey { get; private set; } = string.Empty;
    public SubscriptionStatus Status { get; private set; }
    public DateTimeOffset CurrentPeriodStart { get; private set; }
    public DateTimeOffset CurrentPeriodEnd { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Upgrade(string newPlanKey)
    {
        var newPlan = SubscriptionPlan.FromKey(newPlanKey);
        if (newPlan is null)
        {
            throw new DomainException($"Unknown plan: {newPlanKey}");
        }

        PlanKey = newPlanKey;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Cancel()
    {
        Status = SubscriptionStatus.Cancelled;
        CancelledAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Renew()
    {
        CurrentPeriodStart = CurrentPeriodEnd;
        CurrentPeriodEnd = CurrentPeriodStart.AddMonths(1);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public int GetAssetLimit()
    {
        var plan = SubscriptionPlan.FromKey(PlanKey);
        return plan?.AssetLimit ?? SubscriptionPlan.Free.AssetLimit;
    }
}
