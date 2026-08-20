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
        local.SyncFromStripe(SubscriptionPlan.Business.Key, SubscriptionStatus.Active, webhookAt, webhookAt.AddMonths(1), "sub_1", "cus_1", webhookAt);
        subscriptions.Add(local);
        gateway.NextCanonicalSubscription = new PaymentSubscriptionState(
            "cus_1",
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
        Assert.Contains(activity.Logs, x => x.Action == "subscription.stripe_reconciled");
    }

    [Fact]
    public async Task Reconciliation_RejectsCanonicalAssociationMismatch()
    {
        var subscriptions = new InMemorySubscriptionRepository();
        var activity = new InMemoryActivityLogRepository();
        var gateway = new FakePaymentGateway();
        var clock = new FakeClock { UtcNow = DateTimeOffset.UtcNow };
        var local = new OrganizationSubscription(Guid.NewGuid(), SubscriptionPlan.Business.Key);
        local.SyncFromStripe(SubscriptionPlan.Business.Key, SubscriptionStatus.Active, clock.UtcNow, clock.UtcNow.AddMonths(1), "sub_1", "cus_1", clock.UtcNow);
        subscriptions.Add(local);
        gateway.NextCanonicalSubscription = new PaymentSubscriptionState(
            "cus_other",
            "sub_1",
            SubscriptionPlan.Business.Key,
            SubscriptionStatus.Active,
            clock.UtcNow,
            clock.UtcNow.AddMonths(1),
            local.OrganizationId);

        var service = new SubscriptionReconciliationService(subscriptions, gateway, activity, new FakeUnitOfWork(), clock);
        await service.RunAsync(CancellationToken.None);

        Assert.Equal("cus_1", local.StripeCustomerId);
        Assert.Contains(activity.Logs, x => x.Action == "subscription.stripe_reconciliation_mismatch");
    }
}
