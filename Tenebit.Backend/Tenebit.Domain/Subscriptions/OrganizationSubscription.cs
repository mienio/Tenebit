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
        BillingInterval = BillingInterval.Monthly;
        Status = SubscriptionStatus.Active;
        CurrentPeriodStart = DateTimeOffset.UtcNow;
        CurrentPeriodEnd = CurrentPeriodStart.AddMonths(1);
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string PlanKey { get; private set; } = string.Empty;
    public BillingInterval BillingInterval { get; private set; } = BillingInterval.Monthly;
    public SubscriptionStatus Status { get; private set; }
    public DateTimeOffset CurrentPeriodStart { get; private set; }
    public DateTimeOffset CurrentPeriodEnd { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public string? PaddleCustomerId { get; private set; }
    public string? PaddleSubscriptionId { get; private set; }

    /// <summary>A downgrade in progress: <see cref="PlanKey"/> (and its entitlements) stays on the current,
    /// higher plan - the one the org has already paid for - until <see cref="PendingPlanEffectiveAt"/>, the
    /// end of the paid period.
    ///
    /// Nothing about this is known to Paddle: Paddle can only schedule a cancel/pause, never a plan change,
    /// and every proration_billing_mode swaps the price on the subscription *immediately* (that is exactly
    /// how a downgrade came to take effect the same second it was requested, test report 19.09.2026, błąd
    /// 2). So the switch is held here and pushed to Paddle on its effective date by
    /// SubscriptionReconciliationService.ApplyDuePlanChangesAsync.</summary>
    public string? PendingPlanKey { get; private set; }
    public BillingInterval? PendingBillingInterval { get; private set; }
    public DateTimeOffset? PendingPlanEffectiveAt { get; private set; }

    /// <summary>Timestamp (Paddle webhook `occurred_at`) of the last webhook event actually applied to this
    /// record - Paddle does not guarantee delivery order, so a retried/out-of-order older event must
    /// never overwrite state a newer event already applied (audyt P0.6).</summary>
    public DateTimeOffset? LastWebhookEventAt { get; private set; }

    public bool IsEntitledToPaidPlan => Status == SubscriptionStatus.Active && PlanKey != SubscriptionPlan.Free.Key;

    /// <summary>A provider subscription still exists and must be recovered/managed instead of duplicated.</summary>
    public bool HasLivePaddleSubscription =>
        !string.IsNullOrWhiteSpace(PaddleSubscriptionId) && Status != SubscriptionStatus.Cancelled;

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
        BillingInterval = BillingInterval.Monthly;
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

    public void AttachPaddleCustomer(string paddleCustomerId)
    {
        PaddleCustomerId = paddleCustomerId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Records a downgrade to take effect at <paramref name="effectiveAt"/> (the current period
    /// end). Nothing is sent to Paddle and no entitlement changes until <see cref="ApplyPendingPlanChange"/>
    /// runs on that date; see <see cref="PendingPlanKey"/> for why Paddle cannot hold this itself.</summary>
    public void ScheduleDowngrade(string planKey, BillingInterval interval, DateTimeOffset effectiveAt)
    {
        if (SubscriptionPlan.FromKey(planKey) is null) throw new DomainException($"Unknown plan: {planKey}");

        PendingPlanKey = planKey;
        PendingBillingInterval = interval;
        PendingPlanEffectiveAt = effectiveAt;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>True once the scheduled downgrade is close enough to its effective date to be pushed to
    /// Paddle. Applied slightly early on purpose: the swap has to land before Paddle raises the renewal
    /// invoice, or the org is billed another full period of the plan it asked to leave.</summary>
    public bool IsPlanChangeDue(DateTimeOffset now, TimeSpan lead) =>
        PendingPlanKey is not null && PendingPlanEffectiveAt is not null && now >= PendingPlanEffectiveAt.Value - lead;

    /// <summary>Moves the scheduled plan/interval onto the subscription itself once Paddle has accepted the
    /// switch, and clears the pending state.</summary>
    public void ApplyPendingPlanChange()
    {
        if (PendingPlanKey is null) return;

        var plan = SubscriptionPlan.FromKey(PendingPlanKey);
        if (plan is null) throw new DomainException($"Unknown plan: {PendingPlanKey}");

        PlanKey = plan.Key;
        BillingInterval = PendingBillingInterval ?? BillingInterval;
        PendingPlanKey = null;
        PendingBillingInterval = null;
        PendingPlanEffectiveAt = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ClearPendingPlanChange()
    {
        PendingPlanKey = null;
        PendingBillingInterval = null;
        PendingPlanEffectiveAt = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Applies the state of a Paddle subscription (from checkout completion or a webhook) to this record.
    /// A Cancelled status always reverts the organization to the Free plan, regardless of what plan the
    /// caller passed in - an org can never keep paid-plan benefits once Paddle says the subscription is gone.
    /// </summary>
    public void SyncFromPaddle(string planKey, BillingInterval interval, SubscriptionStatus status, DateTimeOffset currentPeriodStart, DateTimeOffset currentPeriodEnd, string? paddleSubscriptionId, string paddleCustomerId, DateTimeOffset webhookEventCreatedAt)
    {
        // A pending plan/interval change resolves once it has actually landed on Paddle (the canonical
        // planKey+interval catch up to what was scheduled - normally because ApplyDuePlanChangesAsync just
        // pushed it, but an operator editing the subscription in the Paddle dashboard counts too) or the
        // subscription is gone. Until then the canonical plan below is still the *current*, paid-for one,
        // so it is applied as usual and entitlements stay where the customer paid for them.
        if (PendingPlanKey is not null && (status == SubscriptionStatus.Cancelled || (planKey == PendingPlanKey && interval == PendingBillingInterval)))
        {
            PendingPlanKey = null;
            PendingBillingInterval = null;
            PendingPlanEffectiveAt = null;
        }

        PaddleCustomerId = paddleCustomerId;
        PaddleSubscriptionId = paddleSubscriptionId;
        Status = status;

        if (status == SubscriptionStatus.Cancelled)
        {
            PlanKey = SubscriptionPlan.Free.Key;
            BillingInterval = BillingInterval.Monthly;
            CancelledAt ??= DateTimeOffset.UtcNow;
        }
        else if (status is SubscriptionStatus.Unknown or SubscriptionStatus.PastDue)
        {
            PlanKey = SubscriptionPlan.Free.Key;
            BillingInterval = BillingInterval.Monthly;
            CancelledAt ??= DateTimeOffset.UtcNow;
        }
        else
        {
            var plan = SubscriptionPlan.FromKey(planKey);
            if (plan is not null)
            {
                PlanKey = plan.Key;
                BillingInterval = interval;
            }
            CancelledAt = null;
        }

        CurrentPeriodStart = currentPeriodStart;
        CurrentPeriodEnd = currentPeriodEnd;
        LastWebhookEventAt = webhookEventCreatedAt;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ReconcileFromPaddle(string planKey, BillingInterval interval, SubscriptionStatus status, DateTimeOffset currentPeriodStart, DateTimeOffset currentPeriodEnd, string subscriptionId, string paddleCustomerId)
    {
        var lastWebhook = LastWebhookEventAt;
        SyncFromPaddle(planKey, interval, status, currentPeriodStart, currentPeriodEnd, subscriptionId, paddleCustomerId, lastWebhook ?? DateTimeOffset.MinValue);
        LastWebhookEventAt = lastWebhook;
    }
}
