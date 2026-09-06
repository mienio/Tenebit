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
        local.SyncFromPaddle(SubscriptionPlan.Business.Key, SubscriptionStatus.Active, webhookAt, webhookAt.AddMonths(1), "sub_1", "ctm_1", webhookAt);
        subscriptions.Add(local);
        gateway.NextCanonicalSubscription = new PaymentSubscriptionState(
            "ctm_1",
            "sub_1",
            SubscriptionPlan.Free.Key,
            SubscriptionStatus.Unknown,
            clock.UtcNow,
            clock.UtcNow.AddMonths(1),
            local.OrganizationId);

        var service = new SubscriptionReconciliationService(subscriptions, gateway, activity, new FakeUnitOfWork(), clock);
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
        local.SyncFromPaddle(SubscriptionPlan.Business.Key, SubscriptionStatus.Active, clock.UtcNow, clock.UtcNow.AddMonths(1), "sub_1", "ctm_1", clock.UtcNow);
        subscriptions.Add(local);
        gateway.NextCanonicalSubscription = new PaymentSubscriptionState(
            "ctm_other",
            "sub_1",
            SubscriptionPlan.Business.Key,
            SubscriptionStatus.Active,
            clock.UtcNow,
            clock.UtcNow.AddMonths(1),
            local.OrganizationId);

        var service = new SubscriptionReconciliationService(subscriptions, gateway, activity, new FakeUnitOfWork(), clock);
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

        var service = new SubscriptionReconciliationService(subscriptions, gateway, activity, new FakeUnitOfWork(), clock);
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

        var service = new SubscriptionReconciliationService(subscriptions, gateway, activity, new FakeUnitOfWork(), clock);
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
        local.SyncFromPaddle(SubscriptionPlan.Growth.Key, SubscriptionStatus.Active, clock.UtcNow.AddMonths(-1), clock.UtcNow, "sub_1", "ctm_1", clock.UtcNow.AddMonths(-1));
        local.ScheduleDowngrade(SubscriptionPlan.Starter.Key, clock.UtcNow);
        subscriptions.Add(local);
        gateway.NextCanonicalSubscription = new PaymentSubscriptionState(
            "ctm_1", "sub_1", SubscriptionPlan.Starter.Key, SubscriptionStatus.Active,
            clock.UtcNow, clock.UtcNow.AddMonths(1), local.OrganizationId);

        var service = new SubscriptionReconciliationService(subscriptions, gateway, activity, new FakeUnitOfWork(), clock);
        await service.RunAsync(CancellationToken.None);

        Assert.Equal(SubscriptionPlan.Starter.Key, local.PlanKey);
        Assert.Null(local.PendingPlanKey);
        Assert.Null(local.PendingPlanEffectiveAt);
    }
}
