using Tenebit.Application.Abstractions;
using Tenebit.Application.Subscriptions;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Subscriptions;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class SubscriptionServiceTests
{
    private static (SubscriptionService Service, FakeCurrentUser User, InMemoryAssetRepository Assets, InMemorySubscriptionRepository Subscriptions, FakePaymentGateway PaymentGateway, InMemoryProcessedStripeEventRepository ProcessedEvents) CreateService()
    {
        var currentUser = new FakeCurrentUser();
        var assets = new InMemoryAssetRepository();
        var subscriptions = new InMemorySubscriptionRepository();
        var paymentGateway = new FakePaymentGateway();
        var processedEvents = new InMemoryProcessedStripeEventRepository();
        var service = new SubscriptionService(subscriptions, processedEvents, assets, new InMemoryActivityLogRepository(), currentUser, new FakeClock(), new FakeUnitOfWork(), paymentGateway);
        return (service, currentUser, assets, subscriptions, paymentGateway, processedEvents);
    }

    [Fact]
    public async Task GetCurrentAsync_CreatesDefaultFreeSubscriptionWhenNoneExists()
    {
        var (service, _, _, subscriptions, _, _) = CreateService();

        var result = await service.GetCurrentAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("free", result.Value!.PlanKey);
        Assert.Single(subscriptions.Subscriptions);
    }

    [Fact]
    public async Task UpgradeAsync_RejectsNonOwnerRole()
    {
        var (service, user, _, _, _, _) = CreateService();
        user.Roles = ["employee"];

        var result = await service.UpgradeAsync("free", CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task UpgradeAsync_RejectsUnknownPlanKey()
    {
        var (service, _, _, _, _, _) = CreateService();

        var result = await service.UpgradeAsync("enterprise-does-not-exist", CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task UpgradeAsync_RejectsDirectProUpgrade_RequiresStripeCheckout()
    {
        var (service, _, _, _, _, _) = CreateService();

        var result = await service.UpgradeAsync("pro", CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task UpgradeAsync_RejectsFreeDowngradeWhileStripeSubscriptionActive()
    {
        var (service, user, _, subscriptions, _, _) = CreateService();
        var subscription = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Pro.Key);
        subscription.SyncFromStripe(SubscriptionPlan.Pro.Key, SubscriptionStatus.Active, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMonths(1), "sub_123", "cus_123", DateTimeOffset.UtcNow);
        subscriptions.Add(subscription);

        var result = await service.UpgradeAsync("free", CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_RejectsWhenStripeNotConfigured()
    {
        var (service, _, _, _, paymentGateway, _) = CreateService();
        paymentGateway.IsConfigured = false;

        var result = await service.CreateCheckoutSessionAsync("https://app/success", "https://app/cancel", CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_CreatesCustomerAndReturnsCheckoutUrl()
    {
        var (service, _, _, subscriptions, paymentGateway, _) = CreateService();
        paymentGateway.NextCustomerId = "cus_new";
        paymentGateway.NextCheckoutUrl = "https://checkout.stripe.com/session-abc";

        var result = await service.CreateCheckoutSessionAsync("https://app/success", "https://app/cancel", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("https://checkout.stripe.com/session-abc", result.Value);
        Assert.Equal("cus_new", subscriptions.Subscriptions.Single().StripeCustomerId);
    }

    [Fact]
    public async Task HandleWebhookAsync_SyncsPlanFromSubscriptionCreatedEvent()
    {
        var (service, user, _, subscriptions, paymentGateway, _) = CreateService();
        var existing = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Free.Key);
        existing.AttachStripeCustomer("cus_123");
        subscriptions.Add(existing);

        var periodEnd = DateTimeOffset.UtcNow.AddMonths(1);
        paymentGateway.NextWebhookEvent = new PaymentWebhookEvent(
            "evt_created_1", "customer.subscription.created", "cus_123", "sub_123", SubscriptionPlan.Pro.Key, SubscriptionStatus.Active, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, periodEnd, null);

        var result = await service.HandleWebhookAsync("{}", "t=1,v1=fake", CancellationToken.None);

        Assert.True(result.IsSuccess);
        var subscription = subscriptions.Subscriptions.Single();
        Assert.Equal(SubscriptionPlan.Pro.Key, subscription.PlanKey);
        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.Equal("sub_123", subscription.StripeSubscriptionId);
    }

    [Fact]
    public async Task HandleWebhookAsync_RevertsToFreeOnSubscriptionDeletedEvent()
    {
        var (service, user, _, subscriptions, paymentGateway, _) = CreateService();
        var existing = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Pro.Key);
        existing.SyncFromStripe(SubscriptionPlan.Pro.Key, SubscriptionStatus.Active, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMonths(1), "sub_123", "cus_123", DateTimeOffset.UtcNow.AddMinutes(-10));
        subscriptions.Add(existing);

        paymentGateway.NextWebhookEvent = new PaymentWebhookEvent(
            "evt_deleted_1", "customer.subscription.deleted", "cus_123", "sub_123", SubscriptionPlan.Pro.Key, SubscriptionStatus.Cancelled, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);

        var result = await service.HandleWebhookAsync("{}", "t=1,v1=fake", CancellationToken.None);

        Assert.True(result.IsSuccess);
        var subscription = subscriptions.Subscriptions.Single();
        Assert.Equal(SubscriptionPlan.Free.Key, subscription.PlanKey);
        Assert.Equal(SubscriptionStatus.Cancelled, subscription.Status);
    }

    [Fact]
    public async Task HandleWebhookAsync_RejectsInvalidSignature()
    {
        var (service, _, _, _, paymentGateway, _) = CreateService();
        paymentGateway.ThrowOnParseWebhookEvent = true;

        var result = await service.HandleWebhookAsync("{}", "bad-signature", CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task HandleWebhookAsync_ReplayedEventIdIsNoOp()
    {
        var (service, user, _, subscriptions, paymentGateway, processedEvents) = CreateService();
        var existing = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Free.Key);
        existing.AttachStripeCustomer("cus_123");
        subscriptions.Add(existing);

        paymentGateway.NextWebhookEvent = new PaymentWebhookEvent(
            "evt_replay_1", "customer.subscription.created", "cus_123", "sub_123", SubscriptionPlan.Pro.Key,
            SubscriptionStatus.Active, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMonths(1), null);

        var first = await service.HandleWebhookAsync("{}", "t=1,v1=fake", CancellationToken.None);
        Assert.True(first.IsSuccess);
        Assert.Equal(SubscriptionPlan.Pro.Key, subscriptions.Subscriptions.Single().PlanKey);
        Assert.Single(processedEvents.Events);

        // Stripe retries delivery on timeout — replaying the exact same EventId must not reapply/re-log the change.
        var activityCountBefore = subscriptions.Subscriptions.Single().UpdatedAt;
        var second = await service.HandleWebhookAsync("{}", "t=1,v1=fake", CancellationToken.None);

        Assert.True(second.IsSuccess);
        Assert.Single(processedEvents.Events);
        Assert.Equal(activityCountBefore, subscriptions.Subscriptions.Single().UpdatedAt);
    }

    [Fact]
    public async Task HandleWebhookAsync_IgnoresOutOfOrderEventOlderThanLastApplied()
    {
        var (service, user, _, subscriptions, paymentGateway, _) = CreateService();
        var existing = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Free.Key);
        existing.AttachStripeCustomer("cus_123");
        subscriptions.Add(existing);

        var newerEventTime = DateTimeOffset.UtcNow;
        var olderEventTime = newerEventTime.AddMinutes(-10);

        paymentGateway.NextWebhookEvent = new PaymentWebhookEvent(
            "evt_newer", "customer.subscription.created", "cus_123", "sub_123", SubscriptionPlan.Pro.Key,
            SubscriptionStatus.Active, newerEventTime, newerEventTime, newerEventTime.AddMonths(1), null);
        await service.HandleWebhookAsync("{}", "t=1,v1=fake", CancellationToken.None);
        Assert.Equal(SubscriptionPlan.Pro.Key, subscriptions.Subscriptions.Single().PlanKey);

        // A delayed retry of an OLDER event (e.g. the original .created before an .updated already landed)
        // must not revert state a newer event already applied.
        paymentGateway.NextWebhookEvent = new PaymentWebhookEvent(
            "evt_older_retry", "customer.subscription.deleted", "cus_123", "sub_123", SubscriptionPlan.Pro.Key,
            SubscriptionStatus.Cancelled, olderEventTime, olderEventTime, olderEventTime, null);
        var result = await service.HandleWebhookAsync("{}", "t=1,v1=fake", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SubscriptionPlan.Pro.Key, subscriptions.Subscriptions.Single().PlanKey);
        Assert.Equal(SubscriptionStatus.Active, subscriptions.Subscriptions.Single().Status);
    }

    [Fact]
    public async Task CanAddAssetAsync_ReturnsFalseAtFreePlanLimit()
    {
        var (service, user, assets, _, _, _) = CreateService();
        for (var i = 0; i < SubscriptionPlan.Free.AssetLimit; i++)
        {
            assets.Add(new Asset(user.OrganizationId, Guid.NewGuid(), $"Asset {i}", $"AT-{i:000}"));
        }

        var result = await service.CanAddAssetAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value);
    }

    [Fact]
    public async Task CanAddAssetAsync_ReturnsTrueUnderFreePlanLimit()
    {
        var (service, user, assets, _, _, _) = CreateService();
        for (var i = 0; i < 3; i++)
        {
            assets.Add(new Asset(user.OrganizationId, Guid.NewGuid(), $"Asset {i}", $"AT-{i:000}"));
        }

        var result = await service.CanAddAssetAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
    }
}
