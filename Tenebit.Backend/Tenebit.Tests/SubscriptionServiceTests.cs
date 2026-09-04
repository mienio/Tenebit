using Tenebit.Application.Abstractions;
using Tenebit.Application.Subscriptions;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Subscriptions;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class SubscriptionServiceTests
{
    private static (SubscriptionService Service, FakeCurrentUser User, InMemoryAssetRepository Assets, InMemorySubscriptionRepository Subscriptions, FakePaymentGateway PaymentGateway, InMemoryProcessedStripeEventRepository ProcessedEvents, InMemoryPromoCodeRepository PromoCodes) CreateService()
    {
        var currentUser = new FakeCurrentUser();
        var assets = new InMemoryAssetRepository();
        var subscriptions = new InMemorySubscriptionRepository();
        var paymentGateway = new FakePaymentGateway();
        var processedEvents = new InMemoryProcessedStripeEventRepository();
        var promoCodes = new InMemoryPromoCodeRepository();
        var service = new SubscriptionService(
            subscriptions,
            processedEvents,
            assets,
            new InMemoryActivityLogRepository(),
            currentUser,
            new FakeClock(),
            new FakeUnitOfWork(),
            paymentGateway,
            new FakeAppLinkBuilder(),
            promoCodes);
        return (service, currentUser, assets, subscriptions, paymentGateway, processedEvents, promoCodes);
    }

    [Fact]
    public async Task GetCurrentAsync_CreatesDefaultFreeSubscriptionWhenNoneExists()
    {
        var (service, _, _, subscriptions, _, _, _) = CreateService();

        var result = await service.GetCurrentAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("free", result.Value!.PlanKey);
        Assert.Single(subscriptions.Subscriptions);
    }

    [Fact]
    public async Task UpgradeAsync_RejectsNonOwnerRole()
    {
        var (service, user, _, _, _, _, _) = CreateService();
        user.Roles = ["employee"];

        var result = await service.UpgradeAsync("free", CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task UpgradeAsync_RejectsUnknownPlanKey()
    {
        var (service, _, _, _, _, _, _) = CreateService();

        var result = await service.UpgradeAsync("enterprise-does-not-exist", CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task UpgradeAsync_RejectsDirectProUpgrade_RequiresStripeCheckout()
    {
        var (service, _, _, _, _, _, _) = CreateService();

        var result = await service.UpgradeAsync("pro", CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task UpgradeAsync_RejectsFreeDowngradeWhileStripeSubscriptionActive()
    {
        var (service, user, _, subscriptions, _, _, _) = CreateService();
        var subscription = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Business.Key);
        subscription.SyncFromStripe(SubscriptionPlan.Business.Key, SubscriptionStatus.Active, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMonths(1), "sub_123", "cus_123", DateTimeOffset.UtcNow);
        subscriptions.Add(subscription);

        var result = await service.UpgradeAsync("free", CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_RejectsWhenStripeNotConfigured()
    {
        var (service, _, _, _, paymentGateway, _, _) = CreateService();
        paymentGateway.IsConfigured = false;

        var result = await service.CreateCheckoutSessionAsync(SubscriptionPlan.Business.Key, "/success", "/cancel", CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_CreatesCustomerAndReturnsCheckoutUrl()
    {
        var (service, _, _, subscriptions, paymentGateway, _, _) = CreateService();
        paymentGateway.NextCustomerId = "cus_new";
        paymentGateway.NextCheckoutUrl = "https://checkout.stripe.com/session-abc";

        var result = await service.CreateCheckoutSessionAsync(SubscriptionPlan.Business.Key, "/success", "/cancel", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("https://checkout.stripe.com/session-abc", result.Value);
        Assert.Equal("cus_new", subscriptions.Subscriptions.Single().StripeCustomerId);
    }

    [Fact]
    public async Task HandleWebhookAsync_SyncsPlanFromSubscriptionCreatedEvent()
    {
        var (service, user, _, subscriptions, paymentGateway, _, _) = CreateService();
        var existing = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Free.Key);
        existing.AttachStripeCustomer("cus_123");
        subscriptions.Add(existing);

        var periodEnd = DateTimeOffset.UtcNow.AddMonths(1);
        paymentGateway.NextWebhookEvent = new PaymentWebhookEvent(
            "evt_created_1", "customer.subscription.created", "cus_123", "sub_123", SubscriptionPlan.Business.Key, SubscriptionStatus.Active, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, periodEnd, null);

        var result = await service.HandleWebhookAsync("{}", "t=1,v1=fake", CancellationToken.None);

        Assert.True(result.IsSuccess);
        var subscription = subscriptions.Subscriptions.Single();
        Assert.Equal(SubscriptionPlan.Business.Key, subscription.PlanKey);
        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.Equal("sub_123", subscription.StripeSubscriptionId);
    }

    [Fact]
    public async Task HandleWebhookAsync_RevertsToFreeOnSubscriptionDeletedEvent()
    {
        var (service, user, _, subscriptions, paymentGateway, _, _) = CreateService();
        var existing = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Business.Key);
        existing.SyncFromStripe(SubscriptionPlan.Business.Key, SubscriptionStatus.Active, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMonths(1), "sub_123", "cus_123", DateTimeOffset.UtcNow.AddMinutes(-10));
        subscriptions.Add(existing);

        paymentGateway.NextWebhookEvent = new PaymentWebhookEvent(
            "evt_deleted_1", "customer.subscription.deleted", "cus_123", "sub_123", SubscriptionPlan.Business.Key, SubscriptionStatus.Cancelled, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);

        var result = await service.HandleWebhookAsync("{}", "t=1,v1=fake", CancellationToken.None);

        Assert.True(result.IsSuccess);
        var subscription = subscriptions.Subscriptions.Single();
        Assert.Equal(SubscriptionPlan.Free.Key, subscription.PlanKey);
        Assert.Equal(SubscriptionStatus.Cancelled, subscription.Status);
    }

    [Fact]
    public async Task HandleWebhookAsync_RejectsInvalidSignature()
    {
        var (service, _, _, _, paymentGateway, _, _) = CreateService();
        paymentGateway.ThrowOnParseWebhookEvent = true;

        var result = await service.HandleWebhookAsync("{}", "bad-signature", CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task HandleWebhookAsync_ReplayedEventIdIsNoOp()
    {
        var (service, user, _, subscriptions, paymentGateway, processedEvents, _) = CreateService();
        var existing = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Free.Key);
        existing.AttachStripeCustomer("cus_123");
        subscriptions.Add(existing);

        paymentGateway.NextWebhookEvent = new PaymentWebhookEvent(
            "evt_replay_1", "customer.subscription.created", "cus_123", "sub_123", SubscriptionPlan.Business.Key,
            SubscriptionStatus.Active, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMonths(1), null);

        var first = await service.HandleWebhookAsync("{}", "t=1,v1=fake", CancellationToken.None);
        Assert.True(first.IsSuccess);
        Assert.Equal(SubscriptionPlan.Business.Key, subscriptions.Subscriptions.Single().PlanKey);
        Assert.Single(processedEvents.Events);

        // Stripe retries delivery on timeout - replaying the exact same EventId must not reapply/re-log the change.
        var activityCountBefore = subscriptions.Subscriptions.Single().UpdatedAt;
        var second = await service.HandleWebhookAsync("{}", "t=1,v1=fake", CancellationToken.None);

        Assert.True(second.IsSuccess);
        Assert.Single(processedEvents.Events);
        Assert.Equal(activityCountBefore, subscriptions.Subscriptions.Single().UpdatedAt);
    }

    [Fact]
    public async Task HandleWebhookAsync_IgnoresOutOfOrderEventOlderThanLastApplied()
    {
        var (service, user, _, subscriptions, paymentGateway, _, _) = CreateService();
        var existing = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Free.Key);
        existing.AttachStripeCustomer("cus_123");
        subscriptions.Add(existing);

        var newerEventTime = DateTimeOffset.UtcNow;
        var olderEventTime = newerEventTime.AddMinutes(-10);

        paymentGateway.NextWebhookEvent = new PaymentWebhookEvent(
            "evt_newer", "customer.subscription.created", "cus_123", "sub_123", SubscriptionPlan.Business.Key,
            SubscriptionStatus.Active, newerEventTime, newerEventTime, newerEventTime.AddMonths(1), null);
        await service.HandleWebhookAsync("{}", "t=1,v1=fake", CancellationToken.None);
        Assert.Equal(SubscriptionPlan.Business.Key, subscriptions.Subscriptions.Single().PlanKey);

        // A delayed retry of an OLDER event (e.g. the original .created before an .updated already landed)
        // must not revert state a newer event already applied.
        paymentGateway.NextWebhookEvent = new PaymentWebhookEvent(
            "evt_older_retry", "customer.subscription.deleted", "cus_123", "sub_123", SubscriptionPlan.Business.Key,
            SubscriptionStatus.Cancelled, olderEventTime, olderEventTime, olderEventTime, null);
        var result = await service.HandleWebhookAsync("{}", "t=1,v1=fake", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SubscriptionPlan.Business.Key, subscriptions.Subscriptions.Single().PlanKey);
        Assert.Equal(SubscriptionStatus.Active, subscriptions.Subscriptions.Single().Status);
    }

    [Fact]
    public async Task HandleWebhookAsync_UnknownStatus_DoesNotGrantProPlan()
    {
        var (service, user, _, subscriptions, paymentGateway, _, _) = CreateService();
        var existing = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Free.Key);
        existing.AttachStripeCustomer("cus_123");
        subscriptions.Add(existing);

        paymentGateway.NextWebhookEvent = new PaymentWebhookEvent(
            "evt_unknown_1", "customer.subscription.created", "cus_123", "sub_123", SubscriptionPlan.Business.Key,
            SubscriptionStatus.Unknown, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMonths(1), null);

        var result = await service.HandleWebhookAsync("{}", "t=1,v1=fake", CancellationToken.None);

        Assert.True(result.IsSuccess);
        var subscription = subscriptions.Subscriptions.Single();
        Assert.Equal(SubscriptionPlan.Free.Key, subscription.PlanKey);
        Assert.True(subscription.HasLiveStripeSubscription);
    }

    [Fact]
    public async Task HandleWebhookAsync_MetadataOrganizationMismatch_IsRejected()
    {
        var (service, user, _, subscriptions, paymentGateway, _, _) = CreateService();
        var existing = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Free.Key);
        existing.AttachStripeCustomer("cus_owned_by_this_org");
        subscriptions.Add(existing);

        // Metadata routes the event straight to this organization, but the event's actual Stripe
        // customer does not match the customer already attached to it.
        paymentGateway.NextWebhookEvent = new PaymentWebhookEvent(
            "evt_mismatch_1", "customer.subscription.created", "cus_belongs_to_another_org", "sub_999", SubscriptionPlan.Business.Key,
            SubscriptionStatus.Active, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMonths(1), user.OrganizationId);

        var result = await service.HandleWebhookAsync("{}", "t=1,v1=fake", CancellationToken.None);

        Assert.True(result.IsSuccess);
        var subscription = subscriptions.Subscriptions.Single();
        Assert.Equal(SubscriptionPlan.Free.Key, subscription.PlanKey);
        Assert.Equal("cus_owned_by_this_org", subscription.StripeCustomerId);
    }

    [Fact]
    public async Task CanAddAssetAsync_ReturnsFalseAtFreePlanLimit()
    {
        var (service, user, assets, _, _, _, _) = CreateService();
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
        var (service, user, assets, _, _, _, _) = CreateService();
        for (var i = 0; i < 3; i++)
        {
            assets.Add(new Asset(user.OrganizationId, Guid.NewGuid(), $"Asset {i}", $"AT-{i:000}"));
        }

        var result = await service.CanAddAssetAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
    }
    [Theory]
    [InlineData(SubscriptionStatus.PastDue)]
    [InlineData(SubscriptionStatus.Unknown)]
    public async Task CreateCheckoutSessionAsync_BlocksSecondSubscription_WhenProviderSubscriptionStillExists(SubscriptionStatus status)
    {
        var (service, user, _, subscriptions, paymentGateway, _, _) = CreateService();
        var now = DateTimeOffset.UtcNow;
        var existing = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Business.Key);
        existing.SyncFromStripe(SubscriptionPlan.Business.Key, status, now, now.AddMonths(1), "sub_live", "cus_live", now);
        subscriptions.Add(existing);
        paymentGateway.NextCanonicalSubscription = new PaymentSubscriptionState(
            "cus_live", "sub_live", SubscriptionPlan.Free.Key, status, now, now.AddMonths(1), user.OrganizationId);

        var result = await service.CreateCheckoutSessionAsync(SubscriptionPlan.Business.Key, "/success", "/cancel", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(0, paymentGateway.CheckoutCreateCalls);
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_AllowsNewCheckoutAfterCanonicalCancellation()
    {
        var (service, user, _, subscriptions, paymentGateway, _, _) = CreateService();
        var now = DateTimeOffset.UtcNow;
        var existing = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Free.Key);
        existing.SyncFromStripe(SubscriptionPlan.Free.Key, SubscriptionStatus.Cancelled, now, now, "sub_old", "cus_existing", now);
        subscriptions.Add(existing);
        paymentGateway.NextCanonicalSubscription = new PaymentSubscriptionState(
            "cus_existing", "sub_old", SubscriptionPlan.Free.Key, SubscriptionStatus.Cancelled, now, now, user.OrganizationId);

        var result = await service.CreateCheckoutSessionAsync(SubscriptionPlan.Business.Key, "/success", "/cancel", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, paymentGateway.CheckoutCreateCalls);
    }

    [Fact]
    public async Task RepeatedCheckoutAttempt_UsesStableStripeIdempotencyKey()
    {
        var (service, _, _, _, paymentGateway, _, _) = CreateService();

        var first = await service.CreateCheckoutSessionAsync(SubscriptionPlan.Business.Key, "/success", "/cancel", CancellationToken.None);
        var firstKey = paymentGateway.LastCheckoutIdempotencyKey;
        var second = await service.CreateCheckoutSessionAsync(SubscriptionPlan.Business.Key, "/success", "/cancel", CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(firstKey));
        Assert.Equal(firstKey, paymentGateway.LastCheckoutIdempotencyKey);
    }


    [Fact]
    public async Task ValidatePromoCodeAsync_ReturnsDiscountedPrice_ForValidCode()
    {
        var (service, _, _, _, _, _, promoCodes) = CreateService();
        promoCodes.Add(new PromoCode("SUMMER10", SubscriptionPlan.Business.Key, PromoDiscountType.Percentage, 10m, null, null, DateTimeOffset.UtcNow));

        var result = await service.ValidatePromoCodeAsync(SubscriptionPlan.Business.Key, "summer10", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("SUMMER10", result.Value!.Code);
        Assert.Equal(SubscriptionPlan.Business.MonthlyPrice * 0.9m, result.Value.DiscountedPrice);
    }

    [Fact]
    public async Task ValidatePromoCodeAsync_RejectsCodeScopedToAnotherPlan()
    {
        var (service, _, _, _, _, _, promoCodes) = CreateService();
        promoCodes.Add(new PromoCode("GROWTHONLY", SubscriptionPlan.Growth.Key, PromoDiscountType.Percentage, 10m, null, null, DateTimeOffset.UtcNow));

        var result = await service.ValidatePromoCodeAsync(SubscriptionPlan.Business.Key, "GROWTHONLY", CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task ValidatePromoCodeAsync_RejectsExpiredCode()
    {
        var (service, _, _, _, _, _, promoCodes) = CreateService();
        promoCodes.Add(new PromoCode("OLDCODE", SubscriptionPlan.Business.Key, PromoDiscountType.FixedAmount, 5m, null, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(-10)));

        var result = await service.ValidatePromoCodeAsync(SubscriptionPlan.Business.Key, "OLDCODE", CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_RedeemsPromoCodeAndForwardsDiscountToGateway()
    {
        var (service, _, _, _, paymentGateway, _, promoCodes) = CreateService();
        var promo = new PromoCode("LAUNCH20", SubscriptionPlan.Business.Key, PromoDiscountType.Percentage, 20m, 5, null, DateTimeOffset.UtcNow);
        promoCodes.Add(promo);

        var result = await service.CreateCheckoutSessionAsync(SubscriptionPlan.Business.Key, "/success", "/cancel", CancellationToken.None, "launch20");

        Assert.True(result.IsSuccess);
        Assert.Equal(1, promo.TimesRedeemed);
        Assert.NotNull(paymentGateway.LastDiscount);
        Assert.Equal(PromoDiscountType.Percentage, paymentGateway.LastDiscount!.Type);
        Assert.Equal(20m, paymentGateway.LastDiscount.Value);
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_RejectsExhaustedPromoCode_WithoutCreatingCheckout()
    {
        var (service, _, _, _, paymentGateway, _, promoCodes) = CreateService();
        var promo = new PromoCode("ONEUSE", SubscriptionPlan.Business.Key, PromoDiscountType.FixedAmount, 5m, 1, null, DateTimeOffset.UtcNow);
        promo.Redeem();
        promoCodes.Add(promo);

        var result = await service.CreateCheckoutSessionAsync(SubscriptionPlan.Business.Key, "/success", "/cancel", CancellationToken.None, "ONEUSE");

        Assert.True(result.IsFailure);
        Assert.Equal(0, paymentGateway.CheckoutCreateCalls);
    }

    [Fact]
    public async Task ChangePlanAsync_SwitchesLiveSubscriptionToNewPlan()
    {
        var (service, user, _, subscriptions, paymentGateway, _, _) = CreateService();
        var now = DateTimeOffset.UtcNow;
        var local = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Starter.Key);
        local.AttachStripeCustomer("cus_1");
        local.SyncFromStripe(SubscriptionPlan.Starter.Key, SubscriptionStatus.Active, now, now.AddMonths(1), "sub_1", "cus_1", now);
        subscriptions.Add(local);
        paymentGateway.NextChangedSubscription = new PaymentSubscriptionState(
            "cus_1", "sub_1", SubscriptionPlan.Growth.Key, SubscriptionStatus.Active, now, now.AddMonths(1), user.OrganizationId);

        var result = await service.ChangePlanAsync(SubscriptionPlan.Growth.Key, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SubscriptionPlan.Growth.Key, result.Value!.PlanKey);
        Assert.Equal(SubscriptionPlan.Growth.Key, local.PlanKey);
        Assert.Equal(1, paymentGateway.PlanChangeCalls);
        Assert.Equal("sub_1", paymentGateway.LastPlanChangeSubscriptionId);
        Assert.Equal(SubscriptionPlan.Growth.Key, paymentGateway.LastPlanChangeNewPlanKey);
    }

    [Fact]
    public async Task ChangePlanAsync_LeavesPlanUnchanged_WhenStripeDeclinesTheProrationPayment()
    {
        // Regression for the free-upgrade audit finding: a declined card must not leave the org on the
        // higher plan. StripePaymentGateway.ChangeSubscriptionPlanAsync throws a 402 PaymentGatewayException
        // in that case (see StripePaymentGatewayTests); the service must translate that into a normal
        // failure result and must not touch local plan state.
        var (service, user, _, subscriptions, paymentGateway, _, _) = CreateService();
        var now = DateTimeOffset.UtcNow;
        var local = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Starter.Key);
        local.AttachStripeCustomer("cus_1");
        local.SyncFromStripe(SubscriptionPlan.Starter.Key, SubscriptionStatus.Active, now, now.AddMonths(1), "sub_1", "cus_1", now);
        subscriptions.Add(local);
        paymentGateway.ThrowOnPlanChange = new PaymentGatewayException("Stripe API error 402", 402);

        var result = await service.ChangePlanAsync(SubscriptionPlan.Growth.Key, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(SubscriptionPlan.Starter.Key, local.PlanKey);
        Assert.Equal(1, paymentGateway.PlanChangeCalls);
    }

    [Fact]
    public async Task ChangePlanAsync_IsNoOp_WhenAlreadyOnRequestedPlan()
    {
        var (service, user, _, subscriptions, paymentGateway, _, _) = CreateService();
        var now = DateTimeOffset.UtcNow;
        var local = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Growth.Key);
        local.AttachStripeCustomer("cus_1");
        local.SyncFromStripe(SubscriptionPlan.Growth.Key, SubscriptionStatus.Active, now, now.AddMonths(1), "sub_1", "cus_1", now);
        subscriptions.Add(local);

        var result = await service.ChangePlanAsync(SubscriptionPlan.Growth.Key, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, paymentGateway.PlanChangeCalls);
    }

    [Fact]
    public async Task ChangePlanAsync_RejectsWhenNoLiveStripeSubscription()
    {
        var (service, _, _, _, paymentGateway, _, _) = CreateService();

        var result = await service.ChangePlanAsync(SubscriptionPlan.Growth.Key, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(0, paymentGateway.PlanChangeCalls);
    }

    [Fact]
    public async Task ChangePlanAsync_RejectsSwitchingToFree()
    {
        var (service, user, _, subscriptions, paymentGateway, _, _) = CreateService();
        var now = DateTimeOffset.UtcNow;
        var local = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Starter.Key);
        local.AttachStripeCustomer("cus_1");
        local.SyncFromStripe(SubscriptionPlan.Starter.Key, SubscriptionStatus.Active, now, now.AddMonths(1), "sub_1", "cus_1", now);
        subscriptions.Add(local);

        var result = await service.ChangePlanAsync(SubscriptionPlan.Free.Key, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(0, paymentGateway.PlanChangeCalls);
    }

    /// <summary>Limity pozostałych zasobów są egzekwowane, ale nie mogą wyciec przez API - w
    /// odpowiedzi jest wyłącznie licznik aktywów.</summary>
    [Fact]
    public async Task GetCurrentAsync_ReportsAssetUsageOnly()
    {
        var (service, user, assets, _, _, _, _) = CreateService();
        assets.Add(new Asset(user.OrganizationId, Guid.NewGuid(), "Laptop", "AT-001"));

        var result = await service.GetCurrentAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        var usage = result.Value!.Usage;
        Assert.Equal(new[] { "assets" }, usage.Select(x => x.Resource).ToArray());
        Assert.Equal(SubscriptionPlan.Free.AssetLimit, usage[0].Limit);
        Assert.Equal(1, usage[0].Current);
        Assert.Equal(result.Value!.CurrentAssetCount, usage[0].Current);
    }
}
