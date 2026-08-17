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
    public string? StripeCustomerId { get; private set; }
    public string? StripeSubscriptionId { get; private set; }

    /// <summary>Timestamp (Stripe event `created`) of the last webhook event actually applied to this
    /// record — Stripe does not guarantee delivery order, so a retried/out-of-order older event must
    /// never overwrite state a newer event already applied (audyt P0.6).</summary>
    public DateTimeOffset? LastWebhookEventAt { get; private set; }

    /// <summary>True while Stripe still considers there to be a live (billable) subscription behind this plan.</summary>
    public bool HasActiveStripeSubscription =>
        !string.IsNullOrWhiteSpace(StripeSubscriptionId) && Status is SubscriptionStatus.Active or SubscriptionStatus.PastDue;

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

    public void AttachStripeCustomer(string stripeCustomerId)
    {
        StripeCustomerId = stripeCustomerId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Applies the state of a Stripe subscription (from checkout completion or a webhook) to this record.
    /// A Cancelled status always reverts the organization to the Free plan, regardless of what plan the
    /// caller passed in — an org can never keep paid-plan benefits once Stripe says the subscription is gone.
    /// </summary>
    public void SyncFromStripe(string planKey, SubscriptionStatus status, DateTimeOffset currentPeriodStart, DateTimeOffset currentPeriodEnd, string? stripeSubscriptionId, string stripeCustomerId, DateTimeOffset webhookEventCreatedAt)
    {
        StripeCustomerId = stripeCustomerId;
        StripeSubscriptionId = stripeSubscriptionId;
        Status = status;

        if (status == SubscriptionStatus.Cancelled)
        {
            PlanKey = SubscriptionPlan.Free.Key;
            CancelledAt ??= DateTimeOffset.UtcNow;
        }
        else if (status == SubscriptionStatus.Unknown)
        {
            // Fail-closed: quarantine the event without touching PlanKey — an unrecognized status must
            // never grant a paid plan, and GetAssetLimit() reads PlanKey directly regardless of Status
            // (audyt AUD3-010).
        }
        else
        {
            var plan = SubscriptionPlan.FromKey(planKey);
            if (plan is not null) PlanKey = plan.Key;
            CancelledAt = null;
        }

        CurrentPeriodStart = currentPeriodStart;
        CurrentPeriodEnd = currentPeriodEnd;
        LastWebhookEventAt = webhookEventCreatedAt;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
