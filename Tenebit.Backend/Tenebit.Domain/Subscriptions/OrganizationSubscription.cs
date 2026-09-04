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

    /// <summary>A downgrade in progress: <see cref="PlanKey"/> (and its entitlements) stays on the current,
    /// higher plan until <see cref="PendingPlanEffectiveAt"/> - Stripe applies the actual price switch via
    /// a subscription schedule (<see cref="StripeScheduleId"/>) at that date, no local cron needed.</summary>
    public string? PendingPlanKey { get; private set; }
    public DateTimeOffset? PendingPlanEffectiveAt { get; private set; }
    public string? StripeScheduleId { get; private set; }

    /// <summary>Timestamp (Stripe event `created`) of the last webhook event actually applied to this
    /// record - Stripe does not guarantee delivery order, so a retried/out-of-order older event must
    /// never overwrite state a newer event already applied (audyt P0.6).</summary>
    public DateTimeOffset? LastWebhookEventAt { get; private set; }

    public bool IsEntitledToPaidPlan => Status == SubscriptionStatus.Active && PlanKey != SubscriptionPlan.Free.Key;

    /// <summary>A provider subscription still exists and must be recovered/managed instead of duplicated.</summary>
    public bool HasLiveStripeSubscription =>
        !string.IsNullOrWhiteSpace(StripeSubscriptionId) && Status != SubscriptionStatus.Cancelled;

    public Guid? CheckoutAttemptId { get; private set; }
    public DateTimeOffset? CheckoutAttemptExpiresAt { get; private set; }

    public Guid GetOrCreateCheckoutAttempt(DateTimeOffset now, TimeSpan lifetime)
    {
        if (CheckoutAttemptId.HasValue && CheckoutAttemptExpiresAt > now) return CheckoutAttemptId.Value;
        CheckoutAttemptId = Guid.NewGuid();
        CheckoutAttemptExpiresAt = now.Add(lifetime);
        UpdatedAt = now;
        return CheckoutAttemptId.Value;
    }

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

    /// <summary>Cap shared by every other countable resource (people, locations, procedures) - same
    /// number as the plan's asset limit. Not surfaced in the pricing UI; see Terms of Service.</summary>
    public int GetResourceLimit() => GetAssetLimit();

    public void AttachStripeCustomer(string stripeCustomerId)
    {
        StripeCustomerId = stripeCustomerId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Records a downgrade scheduled on Stripe's side to take effect at <paramref name="effectiveAt"/>
    /// (the current period end) - entitlements are untouched until then; see <see cref="SyncFromStripe"/>
    /// for how the pending state clears once Stripe actually applies it.</summary>
    public void ScheduleDowngrade(string planKey, DateTimeOffset effectiveAt, string scheduleId)
    {
        PendingPlanKey = planKey;
        PendingPlanEffectiveAt = effectiveAt;
        StripeScheduleId = scheduleId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ClearPendingPlanChange()
    {
        PendingPlanKey = null;
        PendingPlanEffectiveAt = null;
        StripeScheduleId = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Applies the state of a Stripe subscription (from checkout completion or a webhook) to this record.
    /// A Cancelled status always reverts the organization to the Free plan, regardless of what plan the
    /// caller passed in - an org can never keep paid-plan benefits once Stripe says the subscription is gone.
    /// </summary>
    public void SyncFromStripe(string planKey, SubscriptionStatus status, DateTimeOffset currentPeriodStart, DateTimeOffset currentPeriodEnd, string? stripeSubscriptionId, string stripeCustomerId, DateTimeOffset webhookEventCreatedAt)
    {
        // A pending downgrade resolves itself once Stripe's schedule actually applies the new price (the
        // canonical planKey catches up to what we scheduled) or the subscription is gone - no local cron
        // needed, this just needs to notice either has happened.
        if (PendingPlanKey is not null && (status == SubscriptionStatus.Cancelled || planKey == PendingPlanKey))
        {
            PendingPlanKey = null;
            PendingPlanEffectiveAt = null;
            StripeScheduleId = null;
        }

        StripeCustomerId = stripeCustomerId;
        StripeSubscriptionId = stripeSubscriptionId;
        Status = status;

        if (status == SubscriptionStatus.Cancelled)
        {
            PlanKey = SubscriptionPlan.Free.Key;
            CancelledAt ??= DateTimeOffset.UtcNow;
        }
        else if (status is SubscriptionStatus.Unknown or SubscriptionStatus.PastDue)
        {
            PlanKey = SubscriptionPlan.Free.Key;
            CancelledAt ??= DateTimeOffset.UtcNow;
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

    public void ReconcileFromStripe(string planKey, SubscriptionStatus status, DateTimeOffset currentPeriodStart, DateTimeOffset currentPeriodEnd, string subscriptionId, string stripeCustomerId)
    {
        var lastWebhook = LastWebhookEventAt;
        SyncFromStripe(planKey, status, currentPeriodStart, currentPeriodEnd, subscriptionId, stripeCustomerId, lastWebhook ?? DateTimeOffset.MinValue);
        LastWebhookEventAt = lastWebhook;
    }
}
