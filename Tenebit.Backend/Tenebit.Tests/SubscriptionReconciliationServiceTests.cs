using Microsoft.Extensions.Logging.Abstractions;
using Tenebit.Application.Abstractions;
using Tenebit.Application.Subscriptions;
using Tenebit.Domain.Subscriptions;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public sealed class SubscriptionReconciliationServiceTests
{
    [Fact]
    public async Task Reconciliation_AppliesCanonicalFailClosedState_WithoutAdvancingWebhookOrder()
    {
        var subscriptions = new InMemorySubscriptionRepository();
        var activity = new InMemoryActivityLogRepository();
        var gateway = new FakePaymentGateway();
        var clock = new FakeClock { UtcNow = DateTimeOffset.UtcNow };
        var local = new OrganizationSubscription(Guid.NewGuid(), SubscriptionPlan.Business.Key);
        var webhookAt = clock.UtcNow.AddHours(-1);
        local.SyncFromPaddle(SubscriptionPlan.Business.Key, BillingInterval.Monthly, SubscriptionStatus.Active, webhookAt, webhookAt.AddMonths(1), "sub_1", "ctm_1", webhookAt);
        subscriptions.Add(local);
        gateway.NextCanonicalSubscription = new PaymentSubscriptionState(
            "ctm_1",
            "sub_1",
            SubscriptionPlan.Free.Key,
            SubscriptionStatus.Unknown,
            clock.UtcNow,
            clock.UtcNow.AddMonths(1),
            local.OrganizationId);

        var service = new SubscriptionReconciliationService(subscriptions, gateway, activity, new FakeUnitOfWork(), clock, NullLogger<SubscriptionReconciliationService>.Instance);
        await service.RunAsync(CancellationToken.None);

        Assert.Equal(SubscriptionPlan.Free.Key, local.PlanKey);
        Assert.Equal(SubscriptionStatus.Unknown, local.Status);
        Assert.Equal(SubscriptionPlan.Free.AssetLimit, local.GetAssetLimit());
        Assert.Equal(webhookAt, local.LastWebhookEventAt);
        Assert.Contains(activity.Logs, x => x.Action == "subscription.paddle_reconciled");
    }

    [Fact]
    public async Task Reconciliation_RejectsCanonicalAssociationMismatch()
    {
        var subscriptions = new InMemorySubscriptionRepository();
        var activity = new InMemoryActivityLogRepository();
        var gateway = new FakePaymentGateway();
        var clock = new FakeClock { UtcNow = DateTimeOffset.UtcNow };
        var local = new OrganizationSubscription(Guid.NewGuid(), SubscriptionPlan.Business.Key);
        local.SyncFromPaddle(SubscriptionPlan.Business.Key, BillingInterval.Monthly, SubscriptionStatus.Active, clock.UtcNow, clock.UtcNow.AddMonths(1), "sub_1", "ctm_1", clock.UtcNow);
        subscriptions.Add(local);
        gateway.NextCanonicalSubscription = new PaymentSubscriptionState(
            "ctm_other",
            "sub_1",
            SubscriptionPlan.Business.Key,
            SubscriptionStatus.Active,
            clock.UtcNow,
            clock.UtcNow.AddMonths(1),
            local.OrganizationId);

        var service = new SubscriptionReconciliationService(subscriptions, gateway, activity, new FakeUnitOfWork(), clock, NullLogger<SubscriptionReconciliationService>.Instance);
        await service.RunAsync(CancellationToken.None);

        Assert.Equal("ctm_1", local.PaddleCustomerId);
        Assert.Contains(activity.Logs, x => x.Action == "subscription.paddle_reconciliation_mismatch");
    }

    [Fact]
    public async Task Reconciliation_LinksSubscription_WhenCheckoutCompletedButWebhookNeverArrived()
    {
        var subscriptions = new InMemorySubscriptionRepository();
        var activity = new InMemoryActivityLogRepository();
        var gateway = new FakePaymentGateway();
        var clock = new FakeClock { UtcNow = DateTimeOffset.UtcNow };
        var local = new OrganizationSubscription(Guid.NewGuid(), SubscriptionPlan.Free.Key);
        local.AttachPaddleCustomer("ctm_1");
        subscriptions.Add(local);
        gateway.NextSubscriptionByCustomer = new PaymentSubscriptionState(
            "ctm_1",
            "sub_1",
            SubscriptionPlan.Starter.Key,
            SubscriptionStatus.Active,
            clock.UtcNow,
            clock.UtcNow.AddMonths(1),
            local.OrganizationId);

        var service = new SubscriptionReconciliationService(subscriptions, gateway, activity, new FakeUnitOfWork(), clock, NullLogger<SubscriptionReconciliationService>.Instance);
        await service.RunAsync(CancellationToken.None);

        Assert.Equal(SubscriptionPlan.Starter.Key, local.PlanKey);
        Assert.Equal("sub_1", local.PaddleSubscriptionId);
        Assert.Contains(activity.Logs, x => x.Action == "subscription.paddle_reconciled");
    }

    [Fact]
    public async Task Reconciliation_LeavesFreeCustomerAlone_WhenPaddleHasNoSubscriptionForThem()
    {
        var subscriptions = new InMemorySubscriptionRepository();
        var activity = new InMemoryActivityLogRepository();
        var gateway = new FakePaymentGateway();
        var clock = new FakeClock { UtcNow = DateTimeOffset.UtcNow };
        var local = new OrganizationSubscription(Guid.NewGuid(), SubscriptionPlan.Free.Key);
        local.AttachPaddleCustomer("ctm_1");
        subscriptions.Add(local);

        var service = new SubscriptionReconciliationService(subscriptions, gateway, activity, new FakeUnitOfWork(), clock, NullLogger<SubscriptionReconciliationService>.Instance);
        await service.RunAsync(CancellationToken.None);

        Assert.Equal(SubscriptionPlan.Free.Key, local.PlanKey);
        Assert.Null(local.PaddleSubscriptionId);
        Assert.DoesNotContain(activity.Logs, x => x.Action.StartsWith("subscription.paddle_reconcil"));
    }

    [Fact]
    public async Task Reconciliation_ClearsPendingDowngrade_OncePaddlesScheduledChangeHasApplied()
    {
        // The scheduled change (see PaddlePaymentGateway.ChangeSubscriptionPlanAsync with
        // PlanChangeTiming.NextBillingPeriod) applies the price switch on Paddle's side with no action
        // from us - reconciliation just needs to notice the canonical plan has caught up to what was
        // pending and drop the local "still waiting" markers.
        var subscriptions = new InMemorySubscriptionRepository();
        var activity = new InMemoryActivityLogRepository();
        var gateway = new FakePaymentGateway();
        var clock = new FakeClock { UtcNow = DateTimeOffset.UtcNow };
        var local = new OrganizationSubscription(Guid.NewGuid(), SubscriptionPlan.Growth.Key);
        local.SyncFromPaddle(SubscriptionPlan.Growth.Key, BillingInterval.Monthly, SubscriptionStatus.Active, clock.UtcNow.AddMonths(-1), clock.UtcNow, "sub_1", "ctm_1", clock.UtcNow.AddMonths(-1));
        local.ScheduleDowngrade(SubscriptionPlan.Starter.Key, BillingInterval.Monthly, clock.UtcNow);
        subscriptions.Add(local);
        gateway.NextCanonicalSubscription = new PaymentSubscriptionState(
            "ctm_1", "sub_1", SubscriptionPlan.Starter.Key, SubscriptionStatus.Active,
            clock.UtcNow, clock.UtcNow.AddMonths(1), local.OrganizationId);

        var service = new SubscriptionReconciliationService(subscriptions, gateway, activity, new FakeUnitOfWork(), clock, NullLogger<SubscriptionReconciliationService>.Instance);
        await service.RunAsync(CancellationToken.None);

        Assert.Equal(SubscriptionPlan.Starter.Key, local.PlanKey);
        Assert.Null(local.PendingPlanKey);
        Assert.Null(local.PendingPlanEffectiveAt);
    }

    private static (SubscriptionReconciliationService Service, InMemorySubscriptionRepository Subscriptions, FakePaymentGateway Gateway, InMemoryActivityLogRepository Activity, FakeClock Clock) CreateService()
    {
        var subscriptions = new InMemorySubscriptionRepository();
        var activity = new InMemoryActivityLogRepository();
        var gateway = new FakePaymentGateway();
        var clock = new FakeClock { UtcNow = DateTimeOffset.UtcNow };
        var service = new SubscriptionReconciliationService(subscriptions, gateway, activity, new FakeUnitOfWork(), clock, NullLogger<SubscriptionReconciliationService>.Instance);
        return (service, subscriptions, gateway, activity, clock);
    }

    private static OrganizationSubscription LiveSubscription(FakeClock clock, SubscriptionPlan plan, BillingInterval interval, DateTimeOffset periodEnd)
    {
        var local = new OrganizationSubscription(Guid.NewGuid(), plan.Key);
        local.AttachPaddleCustomer("ctm_1");
        local.SyncFromPaddle(plan.Key, interval, SubscriptionStatus.Active, clock.UtcNow.AddMonths(-1), periodEnd, "sub_1", "ctm_1", clock.UtcNow.AddMonths(-1));
        return local;
    }

    [Fact]
    public async Task ApplyDuePlanChanges_DoesNothing_BeforeTheScheduledDateArrives()
    {
        var (service, subscriptions, gateway, _, clock) = CreateService();
        var periodEnd = clock.UtcNow.AddDays(9);
        var local = LiveSubscription(clock, SubscriptionPlan.Business, BillingInterval.Monthly, periodEnd);
        local.ScheduleDowngrade(SubscriptionPlan.Starter.Key, BillingInterval.Monthly, periodEnd);
        subscriptions.Add(local);

        await service.ApplyDuePlanChangesAsync(CancellationToken.None);

        Assert.Equal(0, gateway.PlanChangeCalls);
        Assert.Equal(SubscriptionPlan.Business.Key, local.PlanKey);
        Assert.Equal(SubscriptionPlan.Business.AssetLimit, local.GetAssetLimit());
        Assert.Equal(SubscriptionPlan.Starter.Key, local.PendingPlanKey);
    }

    [Fact]
    public async Task ApplyDuePlanChanges_SwapsThePriceWithoutCharging_WhenTheBillingCycleIsUnchanged()
    {
        var (service, subscriptions, gateway, activity, clock) = CreateService();
        var periodEnd = clock.UtcNow.AddMinutes(5);
        var local = LiveSubscription(clock, SubscriptionPlan.Business, BillingInterval.Monthly, periodEnd);
        local.ScheduleDowngrade(SubscriptionPlan.Starter.Key, BillingInterval.Monthly, periodEnd);
        subscriptions.Add(local);
        gateway.NextChangedSubscription = new PaymentSubscriptionState(
            "ctm_1", "sub_1", SubscriptionPlan.Starter.Key, SubscriptionStatus.Active, periodEnd, periodEnd.AddMonths(1), local.OrganizationId);

        await service.ApplyDuePlanChangesAsync(CancellationToken.None);

        Assert.Equal(1, gateway.PlanChangeCalls);
        // Nothing is billed today: the renewal that follows simply charges the cheaper plan.
        Assert.Equal(PlanChangeBilling.SwitchWithoutCharge, gateway.LastPlanChangeBilling);
        Assert.Equal(SubscriptionPlan.Starter.Key, local.PlanKey);
        Assert.Null(local.PendingPlanKey);
        Assert.Contains(activity.Logs, x => x.Action == "subscription.plan_change_applied");
    }

    [Fact]
    public async Task ApplyDuePlanChanges_ProratesImmediately_WhenTheBillingCycleChanges()
    {
        // Annual -> monthly: a new, shorter period has to start, which Paddle can only do as an immediate,
        // prorated switch. At the period end there is nothing left to credit, so this costs the org nothing.
        var (service, subscriptions, gateway, _, clock) = CreateService();
        var periodEnd = clock.UtcNow.AddMinutes(5);
        var local = LiveSubscription(clock, SubscriptionPlan.Growth, BillingInterval.Annual, periodEnd);
        local.ScheduleDowngrade(SubscriptionPlan.Growth.Key, BillingInterval.Monthly, periodEnd);
        subscriptions.Add(local);
        gateway.NextChangedSubscription = new PaymentSubscriptionState(
            "ctm_1", "sub_1", SubscriptionPlan.Growth.Key, SubscriptionStatus.Active, periodEnd, periodEnd.AddMonths(1), local.OrganizationId);

        await service.ApplyDuePlanChangesAsync(CancellationToken.None);

        Assert.Equal(PlanChangeBilling.ProrateImmediately, gateway.LastPlanChangeBilling);
        Assert.Equal(BillingInterval.Monthly, local.BillingInterval);
        Assert.Null(local.PendingPlanKey);
    }

    [Fact]
    public async Task ApplyDuePlanChanges_KeepsThePendingChange_WhenPaddleFails()
    {
        // The org must keep the plan it paid for and the switch must be retried, never silently dropped.
        var (service, subscriptions, gateway, activity, clock) = CreateService();
        var periodEnd = clock.UtcNow.AddMinutes(5);
        var local = LiveSubscription(clock, SubscriptionPlan.Business, BillingInterval.Monthly, periodEnd);
        local.ScheduleDowngrade(SubscriptionPlan.Starter.Key, BillingInterval.Monthly, periodEnd);
        subscriptions.Add(local);
        gateway.ThrowOnPlanChange = new PaymentGatewayException("Paddle API error 500", 500);

        await service.ApplyDuePlanChangesAsync(CancellationToken.None);

        Assert.Equal(SubscriptionPlan.Business.Key, local.PlanKey);
        Assert.Equal(SubscriptionPlan.Starter.Key, local.PendingPlanKey);
        Assert.Contains(activity.Logs, x => x.Action == "subscription.plan_change_failed");
    }

    [Fact]
    public async Task ApplyDuePlanChanges_DropsThePendingChange_WhenAnUpgradeAlreadyLandedOnThatPlan()
    {
        var (service, subscriptions, gateway, activity, clock) = CreateService();
        var periodEnd = clock.UtcNow.AddMinutes(5);
        var local = LiveSubscription(clock, SubscriptionPlan.Starter, BillingInterval.Monthly, periodEnd);
        local.ScheduleDowngrade(SubscriptionPlan.Starter.Key, BillingInterval.Monthly, periodEnd);
        subscriptions.Add(local);

        await service.ApplyDuePlanChangesAsync(CancellationToken.None);

        Assert.Equal(0, gateway.PlanChangeCalls);
        Assert.Null(local.PendingPlanKey);
        Assert.Contains(activity.Logs, x => x.Action == "subscription.plan_change_noop");
    }

    [Fact]
    public async Task Reconciliation_RepairsASubscriptionCarryingMoreThanItsOnePlanItem()
    {
        var (service, subscriptions, gateway, activity, clock) = CreateService();
        var local = LiveSubscription(clock, SubscriptionPlan.Business, BillingInterval.Annual, clock.UtcNow.AddYears(1));
        subscriptions.Add(local);
        gateway.NextCanonicalSubscription = new PaymentSubscriptionState(
            "ctm_1", "sub_1", SubscriptionPlan.Business.Key, SubscriptionStatus.Active,
            clock.UtcNow, clock.UtcNow.AddYears(1), local.OrganizationId, BillingInterval.Annual);
        gateway.NextItemRepair = new SubscriptionItemRepair(2, true, true, "2 -> 1 item(s)");

        await service.RunAsync(CancellationToken.None);

        Assert.Equal(1, gateway.EnsureCanonicalItemsCalls);
        Assert.Contains(activity.Logs, x => x.Action == "subscription.paddle_items_repaired");
    }

    [Fact]
    public async Task Reconciliation_LeavesAHealthySubscriptionAlone()
    {
        var (service, subscriptions, gateway, activity, clock) = CreateService();
        var local = LiveSubscription(clock, SubscriptionPlan.Business, BillingInterval.Annual, clock.UtcNow.AddYears(1));
        subscriptions.Add(local);
        gateway.NextCanonicalSubscription = new PaymentSubscriptionState(
            "ctm_1", "sub_1", SubscriptionPlan.Business.Key, SubscriptionStatus.Active,
            clock.UtcNow, clock.UtcNow.AddYears(1), local.OrganizationId, BillingInterval.Annual);

        await service.RunAsync(CancellationToken.None);

        Assert.DoesNotContain(activity.Logs, x => x.Action.StartsWith("subscription.paddle_items_repair"));
    }
}
