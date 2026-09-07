using Microsoft.Extensions.Logging.Abstractions;
using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Application.Subscriptions;
using Tenebit.Domain.Affiliates;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Identity;
using Tenebit.Domain.Organizations;
using Tenebit.Domain.Subscriptions;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class SubscriptionServiceTests
{
    private static (SubscriptionService Service, FakeCurrentUser User, InMemoryAssetRepository Assets, InMemorySubscriptionRepository Subscriptions, FakePaymentGateway PaymentGateway, InMemoryProcessedPaddleEventRepository ProcessedEvents, InMemoryPromoCodeRepository PromoCodes) CreateService() =>
        CreateServiceWithEmail(out _, out _, out _);

    /// <summary>Same wiring as <see cref="CreateService"/>, but also hands back the email plumbing (email
    /// sender, and the organization/owner-user repositories a webhook-triggered congratulations email
    /// looks recipients up in) so a test can assert on what got sent.</summary>
    private static (SubscriptionService Service, FakeCurrentUser User, InMemoryAssetRepository Assets, InMemorySubscriptionRepository Subscriptions, FakePaymentGateway PaymentGateway, InMemoryProcessedPaddleEventRepository ProcessedEvents, InMemoryPromoCodeRepository PromoCodes) CreateServiceWithEmail(
        out FakeEmailSender emailSender, out InMemoryOrganizationRepository organizations, out InMemoryOrganizationUserRepository organizationUsers)
    {
        var currentUser = new FakeCurrentUser();
        var assets = new InMemoryAssetRepository();
        var subscriptions = new InMemorySubscriptionRepository();
        var paymentGateway = new FakePaymentGateway();
        var processedEvents = new InMemoryProcessedPaddleEventRepository();
        var promoCodes = new InMemoryPromoCodeRepository();
        emailSender = new FakeEmailSender();
        organizations = new InMemoryOrganizationRepository();
        organizationUsers = new InMemoryOrganizationUserRepository();
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
            promoCodes,
            organizations,
            organizationUsers,
            emailSender,
            NullLogger<SubscriptionService>.Instance);
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
    public async Task UpgradeAsync_RejectsDirectProUpgrade_RequiresPaddleCheckout()
    {
        var (service, _, _, _, _, _, _) = CreateService();

        var result = await service.UpgradeAsync("pro", CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task UpgradeAsync_RejectsFreeDowngradeWhilePaddleSubscriptionActive()
    {
        var (service, user, _, subscriptions, _, _, _) = CreateService();
        var subscription = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Business.Key);
        subscription.SyncFromPaddle(SubscriptionPlan.Business.Key, SubscriptionStatus.Active, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMonths(1), "sub_123", "ctm_123", DateTimeOffset.UtcNow);
        subscriptions.Add(subscription);

        var result = await service.UpgradeAsync("free", CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task GetCheckoutParamsAsync_RejectsWhenPaddleNotConfigured()
    {
        var (service, _, _, _, paymentGateway, _, _) = CreateService();
        paymentGateway.IsConfigured = false;

        var result = await service.GetCheckoutParamsAsync(SubscriptionPlan.Business.Key, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task GetCheckoutParamsAsync_CreatesCustomerAndReturnsPaddleJsParams()
    {
        var (service, _, _, subscriptions, paymentGateway, _, _) = CreateService();
        paymentGateway.NextCustomerId = "ctm_new";
        paymentGateway.NextCheckoutParams = new PaddleCheckoutParams("pri_business", "ctm_new", null);

        var result = await service.GetCheckoutParamsAsync(SubscriptionPlan.Business.Key, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("pri_business", result.Value!.PriceId);
        Assert.Equal("ctm_new", result.Value.CustomerId);
        Assert.Equal("ctm_new", subscriptions.Subscriptions.Single().PaddleCustomerId);
    }

    private static SubscriptionService CreateServiceWithAffiliateAttribution(
        out FakePaymentGateway paymentGateway, out InMemoryAffiliateCodeRepository affiliateCodes, out InMemoryAffiliateClickRepository affiliateClicks)
    {
        paymentGateway = new FakePaymentGateway();
        affiliateCodes = new InMemoryAffiliateCodeRepository();
        affiliateClicks = new InMemoryAffiliateClickRepository();
        return new SubscriptionService(
            new InMemorySubscriptionRepository(), new InMemoryProcessedPaddleEventRepository(), new InMemoryAssetRepository(),
            new InMemoryActivityLogRepository(), new FakeCurrentUser(), new FakeClock(), new FakeUnitOfWork(), paymentGateway,
            new FakeAppLinkBuilder(), new InMemoryPromoCodeRepository(), new InMemoryOrganizationRepository(), new InMemoryOrganizationUserRepository(),
            new FakeEmailSender(), NullLogger<SubscriptionService>.Instance, affiliateCodes: affiliateCodes, affiliateClicks: affiliateClicks);
    }

    [Fact]
    public async Task GetCheckoutParamsAsync_ResolvesAttributionCookieIntoAffiliateCodeAndForwardsToGateway()
    {
        var service = CreateServiceWithAffiliateAttribution(out var paymentGateway, out var affiliateCodes, out var affiliateClicks);
        var affiliate = new Affiliate("damian@example.com", "hash", "Damian", "Kowalski", "PL", DateTimeOffset.UtcNow);
        affiliate.Approve(DateTimeOffset.UtcNow);
        var code = new AffiliateCode(affiliate.Id, "DAMIAN20", null, DateTimeOffset.UtcNow);
        affiliateCodes.Add(code);
        var attributionToken = Guid.NewGuid();
        affiliateClicks.Add(new AffiliateClick(code.Id, "iphash", null, attributionToken, DateTimeOffset.UtcNow));

        var result = await service.GetCheckoutParamsAsync(SubscriptionPlan.Business.Key, CancellationToken.None, null, attributionToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("DAMIAN20", result.Value!.AffiliateCode);
        Assert.Equal("DAMIAN20", paymentGateway.LastAffiliateCode);
    }

    [Fact]
    public async Task GetCheckoutParamsAsync_IgnoresAnAttributionTokenForADeactivatedCode()
    {
        var service = CreateServiceWithAffiliateAttribution(out var paymentGateway, out var affiliateCodes, out var affiliateClicks);
        var affiliate = new Affiliate("damian@example.com", "hash", "Damian", "Kowalski", "PL", DateTimeOffset.UtcNow);
        affiliate.Approve(DateTimeOffset.UtcNow);
        var code = new AffiliateCode(affiliate.Id, "DAMIAN20", null, DateTimeOffset.UtcNow);
        code.SetActive(false);
        affiliateCodes.Add(code);
        var attributionToken = Guid.NewGuid();
        affiliateClicks.Add(new AffiliateClick(code.Id, "iphash", null, attributionToken, DateTimeOffset.UtcNow));

        var result = await service.GetCheckoutParamsAsync(SubscriptionPlan.Business.Key, CancellationToken.None, null, attributionToken);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.AffiliateCode);
        Assert.Null(paymentGateway.LastAffiliateCode);
    }

    [Fact]
    public async Task GetCheckoutParamsAsync_IgnoresAnUnrecognizedAttributionToken()
    {
        var service = CreateServiceWithAffiliateAttribution(out var paymentGateway, out _, out _);

        var result = await service.GetCheckoutParamsAsync(SubscriptionPlan.Business.Key, CancellationToken.None, null, Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.AffiliateCode);
        Assert.Null(paymentGateway.LastAffiliateCode);
    }

    [Fact]
    public async Task HandleWebhookAsync_IgnoresTransactionCompletedEvents_LeavingEntitlementUntouched()
    {
        // Affiliate commission wiring reads transaction.completed independently
        // (AffiliateConversionRecordingService.HandleWebhookAsync) - this method must never let it reach
        // SyncFromPaddle, whose Status/PlanKey/CurrentPeriod* fields are meaningless placeholders on that
        // event shape (see PaddlePaymentGatewayTests.ParseWebhookEvent_TransactionCompleted_DoesNotTouchSubscriptionEntitlementFields).
        var (service, user, _, subscriptions, paymentGateway, _, _) = CreateService();
        var existing = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Business.Key);
        existing.AttachPaddleCustomer("ctm_123");
        subscriptions.Add(existing);

        paymentGateway.NextWebhookEvent = new PaymentWebhookEvent(
            "ntf_txn_1", "transaction.completed", "ctm_123", "sub_123", "", SubscriptionStatus.Unknown,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null,
            "txn_1", "DAMIAN20", 100m, 90m, "EUR", false);

        var result = await service.HandleWebhookAsync("{}", "sig", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SubscriptionPlan.Business.Key, subscriptions.Subscriptions.Single().PlanKey);
    }

    [Fact]
    public async Task HandleWebhookAsync_SyncsPlanFromSubscriptionCreatedEvent()
    {
        var (service, user, _, subscriptions, paymentGateway, _, _) = CreateService();
        var existing = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Free.Key);
        existing.AttachPaddleCustomer("ctm_123");
        subscriptions.Add(existing);

        var periodEnd = DateTimeOffset.UtcNow.AddMonths(1);
        paymentGateway.NextWebhookEvent = new PaymentWebhookEvent(
            "ntf_created_1", "subscription.created", "ctm_123", "sub_123", SubscriptionPlan.Business.Key, SubscriptionStatus.Active, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, periodEnd, null);

        var result = await service.HandleWebhookAsync("{}", "ts=1;h1=fake", CancellationToken.None);

        Assert.True(result.IsSuccess);
        var subscription = subscriptions.Subscriptions.Single();
        Assert.Equal(SubscriptionPlan.Business.Key, subscription.PlanKey);
        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.Equal("sub_123", subscription.PaddleSubscriptionId);
    }

    [Fact]
    public async Task HandleWebhookAsync_RevertsToFreeOnSubscriptionCanceledEvent()
    {
        var (service, user, _, subscriptions, paymentGateway, _, _) = CreateService();
        var existing = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Business.Key);
        existing.SyncFromPaddle(SubscriptionPlan.Business.Key, SubscriptionStatus.Active, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMonths(1), "sub_123", "ctm_123", DateTimeOffset.UtcNow.AddMinutes(-10));
        subscriptions.Add(existing);

        paymentGateway.NextWebhookEvent = new PaymentWebhookEvent(
            "ntf_canceled_1", "subscription.canceled", "ctm_123", "sub_123", SubscriptionPlan.Business.Key, SubscriptionStatus.Cancelled, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);

        var result = await service.HandleWebhookAsync("{}", "ts=1;h1=fake", CancellationToken.None);

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
        existing.AttachPaddleCustomer("ctm_123");
        subscriptions.Add(existing);

        paymentGateway.NextWebhookEvent = new PaymentWebhookEvent(
            "ntf_replay_1", "subscription.created", "ctm_123", "sub_123", SubscriptionPlan.Business.Key,
            SubscriptionStatus.Active, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMonths(1), null);

        var first = await service.HandleWebhookAsync("{}", "ts=1;h1=fake", CancellationToken.None);
        Assert.True(first.IsSuccess);
        Assert.Equal(SubscriptionPlan.Business.Key, subscriptions.Subscriptions.Single().PlanKey);
        Assert.Single(processedEvents.Events);

        // Paddle retries delivery on timeout - replaying the exact same notification_id must not reapply/re-log the change.
        var activityCountBefore = subscriptions.Subscriptions.Single().UpdatedAt;
        var second = await service.HandleWebhookAsync("{}", "ts=1;h1=fake", CancellationToken.None);

        Assert.True(second.IsSuccess);
        Assert.Single(processedEvents.Events);
        Assert.Equal(activityCountBefore, subscriptions.Subscriptions.Single().UpdatedAt);
    }

    [Fact]
    public async Task HandleWebhookAsync_IgnoresOutOfOrderEventOlderThanLastApplied()
    {
        var (service, user, _, subscriptions, paymentGateway, _, _) = CreateService();
        var existing = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Free.Key);
        existing.AttachPaddleCustomer("ctm_123");
        subscriptions.Add(existing);

        var newerEventTime = DateTimeOffset.UtcNow;
        var olderEventTime = newerEventTime.AddMinutes(-10);

        paymentGateway.NextWebhookEvent = new PaymentWebhookEvent(
            "ntf_newer", "subscription.created", "ctm_123", "sub_123", SubscriptionPlan.Business.Key,
            SubscriptionStatus.Active, newerEventTime, newerEventTime, newerEventTime.AddMonths(1), null);
        await service.HandleWebhookAsync("{}", "ts=1;h1=fake", CancellationToken.None);
        Assert.Equal(SubscriptionPlan.Business.Key, subscriptions.Subscriptions.Single().PlanKey);

        // A delayed retry of an OLDER event (e.g. the original .created before an .updated already landed)
        // must not revert state a newer event already applied.
        paymentGateway.NextWebhookEvent = new PaymentWebhookEvent(
            "ntf_older_retry", "subscription.canceled", "ctm_123", "sub_123", SubscriptionPlan.Business.Key,
            SubscriptionStatus.Cancelled, olderEventTime, olderEventTime, olderEventTime, null);
        var result = await service.HandleWebhookAsync("{}", "ts=1;h1=fake", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SubscriptionPlan.Business.Key, subscriptions.Subscriptions.Single().PlanKey);
        Assert.Equal(SubscriptionStatus.Active, subscriptions.Subscriptions.Single().Status);
    }

    [Fact]
    public async Task HandleWebhookAsync_UnknownStatus_DoesNotGrantProPlan()
    {
        var (service, user, _, subscriptions, paymentGateway, _, _) = CreateService();
        var existing = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Free.Key);
        existing.AttachPaddleCustomer("ctm_123");
        subscriptions.Add(existing);

        paymentGateway.NextWebhookEvent = new PaymentWebhookEvent(
            "ntf_unknown_1", "subscription.created", "ctm_123", "sub_123", SubscriptionPlan.Business.Key,
            SubscriptionStatus.Unknown, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMonths(1), null);

        var result = await service.HandleWebhookAsync("{}", "ts=1;h1=fake", CancellationToken.None);

        Assert.True(result.IsSuccess);
        var subscription = subscriptions.Subscriptions.Single();
        Assert.Equal(SubscriptionPlan.Free.Key, subscription.PlanKey);
        Assert.True(subscription.HasLivePaddleSubscription);
    }

    [Fact]
    public async Task HandleWebhookAsync_MetadataOrganizationMismatch_IsRejected()
    {
        var (service, user, _, subscriptions, paymentGateway, _, _) = CreateService();
        var existing = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Free.Key);
        existing.AttachPaddleCustomer("ctm_owned_by_this_org");
        subscriptions.Add(existing);

        // Even if a caller supplied an organizationId, the event's actual Paddle customer must still
        // match the customer already attached to this record.
        paymentGateway.NextWebhookEvent = new PaymentWebhookEvent(
            "ntf_mismatch_1", "subscription.created", "ctm_belongs_to_another_org", "sub_999", SubscriptionPlan.Business.Key,
            SubscriptionStatus.Active, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMonths(1), user.OrganizationId);

        var result = await service.HandleWebhookAsync("{}", "ts=1;h1=fake", CancellationToken.None);

        Assert.True(result.IsSuccess);
        var subscription = subscriptions.Subscriptions.Single();
        Assert.Equal(SubscriptionPlan.Free.Key, subscription.PlanKey);
        Assert.Equal("ctm_owned_by_this_org", subscription.PaddleCustomerId);
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
    public async Task GetCheckoutParamsAsync_BlocksSecondSubscription_WhenProviderSubscriptionStillExists(SubscriptionStatus status)
    {
        var (service, user, _, subscriptions, paymentGateway, _, _) = CreateService();
        var now = DateTimeOffset.UtcNow;
        var existing = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Business.Key);
        existing.SyncFromPaddle(SubscriptionPlan.Business.Key, status, now, now.AddMonths(1), "sub_live", "ctm_live", now);
        subscriptions.Add(existing);
        paymentGateway.NextCanonicalSubscription = new PaymentSubscriptionState(
            "ctm_live", "sub_live", SubscriptionPlan.Free.Key, status, now, now.AddMonths(1), user.OrganizationId);

        var result = await service.GetCheckoutParamsAsync(SubscriptionPlan.Business.Key, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(0, paymentGateway.CheckoutCreateCalls);
    }

    [Fact]
    public async Task GetCheckoutParamsAsync_AllowsNewCheckoutAfterCanonicalCancellation()
    {
        var (service, user, _, subscriptions, paymentGateway, _, _) = CreateService();
        var now = DateTimeOffset.UtcNow;
        var existing = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Free.Key);
        existing.SyncFromPaddle(SubscriptionPlan.Free.Key, SubscriptionStatus.Cancelled, now, now, "sub_old", "ctm_existing", now);
        subscriptions.Add(existing);
        paymentGateway.NextCanonicalSubscription = new PaymentSubscriptionState(
            "ctm_existing", "sub_old", SubscriptionPlan.Free.Key, SubscriptionStatus.Cancelled, now, now, user.OrganizationId);

        var result = await service.GetCheckoutParamsAsync(SubscriptionPlan.Business.Key, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, paymentGateway.CheckoutCreateCalls);
    }

    [Fact]
    public async Task RepeatedCheckoutAttempt_DoesNotCreateASecondPaddleCustomer()
    {
        var (service, _, _, subscriptions, paymentGateway, _, _) = CreateService();

        var first = await service.GetCheckoutParamsAsync(SubscriptionPlan.Business.Key, CancellationToken.None);
        var second = await service.GetCheckoutParamsAsync(SubscriptionPlan.Business.Key, CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(1, subscriptions.Subscriptions.Count);
        Assert.Equal(paymentGateway.NextCustomerId, subscriptions.Subscriptions.Single().PaddleCustomerId);
    }

    [Fact]
    public async Task ValidatePromoCodeAsync_ReturnsDiscountedPrice_ForValidCode()
    {
        var (service, _, _, _, _, _, promoCodes) = CreateService();
        promoCodes.Add(new PromoCode("SUMMER10", SubscriptionPlan.Business.Key, PromoDiscountType.Percentage, 10m, null, null, PromoDurationType.Once, null, null, DateTimeOffset.UtcNow));

        var result = await service.ValidatePromoCodeAsync(SubscriptionPlan.Business.Key, "summer10", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("SUMMER10", result.Value!.Code);
        Assert.Equal(Math.Round(SubscriptionPlan.Business.MonthlyPrice * 0.9m, 2), result.Value.DiscountedPrice);
    }

    [Fact]
    public async Task ValidatePromoCodeAsync_RejectsCodeScopedToAnotherPlan()
    {
        var (service, _, _, _, _, _, promoCodes) = CreateService();
        promoCodes.Add(new PromoCode("GROWTHONLY", SubscriptionPlan.Growth.Key, PromoDiscountType.Percentage, 10m, null, null, PromoDurationType.Once, null, null, DateTimeOffset.UtcNow));

        var result = await service.ValidatePromoCodeAsync(SubscriptionPlan.Business.Key, "GROWTHONLY", CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task ValidatePromoCodeAsync_RejectsExpiredCode()
    {
        var (service, _, _, _, _, _, promoCodes) = CreateService();
        promoCodes.Add(new PromoCode("OLDCODE", SubscriptionPlan.Business.Key, PromoDiscountType.FixedAmount, 5m, null, DateTimeOffset.UtcNow.AddDays(-1), PromoDurationType.Once, null, null, DateTimeOffset.UtcNow.AddDays(-10)));

        var result = await service.ValidatePromoCodeAsync(SubscriptionPlan.Business.Key, "OLDCODE", CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task ValidatePromoCodeAsync_RejectsADiscountThatDropsBelowPaddlesMinimumChargeableAmount()
    {
        // Verified against the real Paddle sandbox API: a transaction at or below ~0.70 USD is rejected
        // outright (transaction_balance_less_than_charge_limit) - a 99%-off code on an already-cheap plan
        // must be caught here with a clear message, not surfaced as a raw Paddle API error mid-checkout.
        var (service, _, _, _, _, _, promoCodes) = CreateService();
        promoCodes.Add(new PromoCode("TOOSTEEP", SubscriptionPlan.Starter.Key, PromoDiscountType.Percentage, 99m, null, null, PromoDurationType.Once, null, null, DateTimeOffset.UtcNow));

        var result = await service.ValidatePromoCodeAsync(SubscriptionPlan.Starter.Key, "TOOSTEEP", CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task GetCheckoutParamsAsync_RejectsADiscountThatDropsBelowPaddlesMinimumChargeableAmount_WithoutCreatingCheckout()
    {
        var (service, _, _, _, paymentGateway, _, promoCodes) = CreateService();
        promoCodes.Add(new PromoCode("TOOSTEEP", SubscriptionPlan.Starter.Key, PromoDiscountType.Percentage, 99m, null, null, PromoDurationType.Once, null, null, DateTimeOffset.UtcNow));

        var result = await service.GetCheckoutParamsAsync(SubscriptionPlan.Starter.Key, CancellationToken.None, "TOOSTEEP");

        Assert.True(result.IsFailure);
        Assert.Equal(0, paymentGateway.CheckoutCreateCalls);
    }

    [Fact]
    public async Task GetCheckoutParamsAsync_RedeemsPromoCodeAndForwardsDiscountToGateway()
    {
        var (service, _, _, _, paymentGateway, _, promoCodes) = CreateService();
        var promo = new PromoCode("LAUNCH20", SubscriptionPlan.Business.Key, PromoDiscountType.Percentage, 20m, 5, null, PromoDurationType.Once, null, null, DateTimeOffset.UtcNow);
        promoCodes.Add(promo);

        var result = await service.GetCheckoutParamsAsync(SubscriptionPlan.Business.Key, CancellationToken.None, "launch20");

        Assert.True(result.IsSuccess);
        Assert.Equal(1, promo.TimesRedeemed);
        Assert.NotNull(paymentGateway.LastDiscount);
        Assert.Equal(PromoDiscountType.Percentage, paymentGateway.LastDiscount!.Type);
        Assert.Equal(20m, paymentGateway.LastDiscount.Value);
    }

    [Fact]
    public async Task GetCheckoutParamsAsync_RejectsExhaustedPromoCode_WithoutCreatingCheckout()
    {
        var (service, _, _, _, paymentGateway, _, promoCodes) = CreateService();
        var promo = new PromoCode("ONEUSE", SubscriptionPlan.Business.Key, PromoDiscountType.FixedAmount, 5m, 1, null, PromoDurationType.Once, null, null, DateTimeOffset.UtcNow);
        promo.Redeem();
        promoCodes.Add(promo);

        var result = await service.GetCheckoutParamsAsync(SubscriptionPlan.Business.Key, CancellationToken.None, "ONEUSE");

        Assert.True(result.IsFailure);
        Assert.Equal(0, paymentGateway.CheckoutCreateCalls);
    }

    [Fact]
    public async Task ChangePlanAsync_SwitchesLiveSubscriptionToNewPlan_Immediately()
    {
        var (service, user, _, subscriptions, paymentGateway, _, _) = CreateService();
        var now = DateTimeOffset.UtcNow;
        var local = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Starter.Key);
        local.AttachPaddleCustomer("ctm_1");
        local.SyncFromPaddle(SubscriptionPlan.Starter.Key, SubscriptionStatus.Active, now, now.AddMonths(1), "sub_1", "ctm_1", now);
        subscriptions.Add(local);
        paymentGateway.NextChangedSubscription = new PaymentSubscriptionState(
            "ctm_1", "sub_1", SubscriptionPlan.Growth.Key, SubscriptionStatus.Active, now, now.AddMonths(1), user.OrganizationId);

        var result = await service.ChangePlanAsync(SubscriptionPlan.Growth.Key, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SubscriptionPlan.Growth.Key, result.Value!.PlanKey);
        Assert.Equal(SubscriptionPlan.Growth.Key, local.PlanKey);
        Assert.Equal(1, paymentGateway.PlanChangeCalls);
        Assert.Equal(PlanChangeTiming.Immediately, paymentGateway.LastPlanChangeTiming);
        Assert.Equal("sub_1", paymentGateway.LastPlanChangeSubscriptionId);
        Assert.Equal(SubscriptionPlan.Growth.Key, paymentGateway.LastPlanChangeNewPlanKey);
    }

    [Fact]
    public async Task PreviewPlanChangeAsync_ReturnsPaddlesExactProrationAmount_ForAnUpgrade()
    {
        var (service, user, _, subscriptions, paymentGateway, _, _) = CreateService();
        var now = DateTimeOffset.UtcNow;
        var local = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Starter.Key);
        local.AttachPaddleCustomer("ctm_1");
        local.SyncFromPaddle(SubscriptionPlan.Starter.Key, SubscriptionStatus.Active, now, now.AddMonths(1), "sub_1", "ctm_1", now);
        subscriptions.Add(local);
        paymentGateway.NextPlanChangePreview = new PlanChangePreview(20.88m, "EUR", null);

        var result = await service.PreviewPlanChangeAsync(SubscriptionPlan.Growth.Key, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.ChargesNow);
        Assert.Equal(20.88m, result.Value.AmountDue);
        Assert.Equal("EUR", result.Value.Currency);
        Assert.Null(result.Value.EffectiveAt);
        Assert.Equal(PlanChangeTiming.Immediately, paymentGateway.LastPreviewTiming);
    }

    [Fact]
    public async Task PreviewPlanChangeAsync_ReturnsNoChargeAndTheEffectiveDate_ForADowngrade()
    {
        var (service, user, _, subscriptions, paymentGateway, _, _) = CreateService();
        var now = DateTimeOffset.UtcNow;
        var periodEnd = now.AddDays(12);
        var local = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Growth.Key);
        local.AttachPaddleCustomer("ctm_1");
        local.SyncFromPaddle(SubscriptionPlan.Growth.Key, SubscriptionStatus.Active, now, periodEnd, "sub_1", "ctm_1", now);
        subscriptions.Add(local);
        paymentGateway.NextPlanChangePreview = new PlanChangePreview(0m, "EUR", periodEnd);

        var result = await service.PreviewPlanChangeAsync(SubscriptionPlan.Starter.Key, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.ChargesNow);
        Assert.Equal(0m, result.Value.AmountDue);
        Assert.Equal(periodEnd, result.Value.EffectiveAt);
        Assert.Equal(PlanChangeTiming.NextBillingPeriod, paymentGateway.LastPreviewTiming);
    }

    [Fact]
    public async Task ChangePlanAsync_ReportsTheExactAmountPaddleActuallyCharged()
    {
        var (service, user, _, subscriptions, paymentGateway, _, _) = CreateService();
        var now = DateTimeOffset.UtcNow;
        var local = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Starter.Key);
        local.AttachPaddleCustomer("ctm_1");
        local.SyncFromPaddle(SubscriptionPlan.Starter.Key, SubscriptionStatus.Active, now, now.AddMonths(1), "sub_1", "ctm_1", now);
        subscriptions.Add(local);
        paymentGateway.NextChangedSubscription = new PaymentSubscriptionState(
            "ctm_1", "sub_1", SubscriptionPlan.Growth.Key, SubscriptionStatus.Active, now, now.AddMonths(1), user.OrganizationId);
        paymentGateway.NextChargedAmount = 20.88m;
        paymentGateway.NextChargedCurrency = "EUR";

        var result = await service.ChangePlanAsync(SubscriptionPlan.Growth.Key, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(20.88m, result.Value!.LastChargeAmount);
        Assert.Equal("EUR", result.Value.LastChargeCurrency);
    }

    [Fact]
    public async Task ChangePlanAsync_RedeemsPromoCodeAndForwardsDiscountToGateway()
    {
        var (service, user, _, subscriptions, paymentGateway, _, promoCodes) = CreateService();
        var now = DateTimeOffset.UtcNow;
        var local = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Starter.Key);
        local.AttachPaddleCustomer("ctm_1");
        local.SyncFromPaddle(SubscriptionPlan.Starter.Key, SubscriptionStatus.Active, now, now.AddMonths(1), "sub_1", "ctm_1", now);
        subscriptions.Add(local);
        paymentGateway.NextChangedSubscription = new PaymentSubscriptionState(
            "ctm_1", "sub_1", SubscriptionPlan.Growth.Key, SubscriptionStatus.Active, now, now.AddMonths(1), user.OrganizationId);
        var promo = new PromoCode("UPGRADE20", SubscriptionPlan.Growth.Key, PromoDiscountType.Percentage, 20m, 5, null, PromoDurationType.Once, null, null, now);
        promoCodes.Add(promo);

        var result = await service.ChangePlanAsync(SubscriptionPlan.Growth.Key, CancellationToken.None, "upgrade20");

        Assert.True(result.IsSuccess);
        Assert.Equal(1, promo.TimesRedeemed);
        Assert.NotNull(paymentGateway.LastPlanChangeDiscount);
        Assert.Equal(PromoDiscountType.Percentage, paymentGateway.LastPlanChangeDiscount!.Type);
        Assert.Equal(20m, paymentGateway.LastPlanChangeDiscount.Value);
    }

    [Fact]
    public async Task ChangePlanAsync_RejectsExhaustedPromoCode_WithoutChangingPlan()
    {
        var (service, user, _, subscriptions, paymentGateway, _, promoCodes) = CreateService();
        var now = DateTimeOffset.UtcNow;
        var local = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Starter.Key);
        local.AttachPaddleCustomer("ctm_1");
        local.SyncFromPaddle(SubscriptionPlan.Starter.Key, SubscriptionStatus.Active, now, now.AddMonths(1), "sub_1", "ctm_1", now);
        subscriptions.Add(local);
        var promo = new PromoCode("SPENT", SubscriptionPlan.Growth.Key, PromoDiscountType.FixedAmount, 5m, 1, null, PromoDurationType.Once, null, null, now);
        promo.Redeem();
        promoCodes.Add(promo);

        var result = await service.ChangePlanAsync(SubscriptionPlan.Growth.Key, CancellationToken.None, "SPENT");

        Assert.True(result.IsFailure);
        Assert.Equal(0, paymentGateway.PlanChangeCalls);
        Assert.Equal(SubscriptionPlan.Starter.Key, local.PlanKey);
    }

    [Fact]
    public async Task ChangePlanAsync_DoesNotRedeemPromoCode_ForADowngrade()
    {
        // A deferred downgrade charges nothing today, so there is nothing for a promo code to discount -
        // ChangePlanAsync must not even look the code up on that path.
        var (service, user, _, subscriptions, paymentGateway, _, promoCodes) = CreateService();
        var now = DateTimeOffset.UtcNow;
        var periodEnd = now.AddMonths(1);
        var local = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Growth.Key);
        local.AttachPaddleCustomer("ctm_1");
        local.SyncFromPaddle(SubscriptionPlan.Growth.Key, SubscriptionStatus.Active, now, periodEnd, "sub_1", "ctm_1", now);
        subscriptions.Add(local);
        paymentGateway.NextChangedSubscription = new PaymentSubscriptionState(
            "ctm_1", "sub_1", SubscriptionPlan.Growth.Key, SubscriptionStatus.Active, now, periodEnd, user.OrganizationId);
        paymentGateway.NextPendingEffectiveAt = periodEnd;
        var promo = new PromoCode("IGNORED", SubscriptionPlan.Starter.Key, PromoDiscountType.Percentage, 20m, 5, null, PromoDurationType.Once, null, null, now);
        promoCodes.Add(promo);

        var result = await service.ChangePlanAsync(SubscriptionPlan.Starter.Key, CancellationToken.None, "IGNORED");

        Assert.True(result.IsSuccess);
        Assert.Equal(0, promo.TimesRedeemed);
        Assert.Null(paymentGateway.LastPlanChangeDiscount);
    }

    [Fact]
    public async Task ChangePlanAsync_LeavesPlanUnchanged_WhenPaddleDeclinesTheProrationPayment()
    {
        // Regression for the free-upgrade audit finding: a declined card must not leave the org on the
        // higher plan. PaddlePaymentGateway.ChangeSubscriptionPlanAsync throws a 402 PaymentGatewayException
        // in that case (see PaddlePaymentGatewayTests); the service must translate that into a normal
        // failure result and must not touch local plan state.
        var (service, user, _, subscriptions, paymentGateway, _, _) = CreateService();
        var now = DateTimeOffset.UtcNow;
        var local = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Starter.Key);
        local.AttachPaddleCustomer("ctm_1");
        local.SyncFromPaddle(SubscriptionPlan.Starter.Key, SubscriptionStatus.Active, now, now.AddMonths(1), "sub_1", "ctm_1", now);
        subscriptions.Add(local);
        paymentGateway.ThrowOnPlanChange = new PaymentGatewayException("Paddle did not apply the plan change (payment likely failed).", 402);

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
        local.AttachPaddleCustomer("ctm_1");
        local.SyncFromPaddle(SubscriptionPlan.Growth.Key, SubscriptionStatus.Active, now, now.AddMonths(1), "sub_1", "ctm_1", now);
        subscriptions.Add(local);

        var result = await service.ChangePlanAsync(SubscriptionPlan.Growth.Key, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, paymentGateway.PlanChangeCalls);
    }

    [Fact]
    public async Task ChangePlanAsync_RejectsWhenNoLivePaddleSubscription()
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
        local.AttachPaddleCustomer("ctm_1");
        local.SyncFromPaddle(SubscriptionPlan.Starter.Key, SubscriptionStatus.Active, now, now.AddMonths(1), "sub_1", "ctm_1", now);
        subscriptions.Add(local);

        var result = await service.ChangePlanAsync(SubscriptionPlan.Free.Key, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(0, paymentGateway.PlanChangeCalls);
    }

    [Fact]
    public async Task ChangePlanAsync_SchedulesDowngrade_InsteadOfSwitchingImmediately()
    {
        // A downgrade must not take the org off its current (higher) plan - and its entitlements - until
        // the period already paid for actually ends. ChangePlanAsync must tell Paddle to apply it at the
        // next billing period (proration_billing_mode=full_next_billing_period), never bill anything now.
        var (service, user, _, subscriptions, paymentGateway, _, _) = CreateService();
        var now = DateTimeOffset.UtcNow;
        var periodEnd = now.AddMonths(1);
        var local = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Growth.Key);
        local.AttachPaddleCustomer("ctm_1");
        local.SyncFromPaddle(SubscriptionPlan.Growth.Key, SubscriptionStatus.Active, now, periodEnd, "sub_1", "ctm_1", now);
        subscriptions.Add(local);
        paymentGateway.NextChangedSubscription = new PaymentSubscriptionState(
            "ctm_1", "sub_1", SubscriptionPlan.Growth.Key, SubscriptionStatus.Active, now, periodEnd, user.OrganizationId);
        paymentGateway.NextPendingEffectiveAt = periodEnd;

        var result = await service.ChangePlanAsync(SubscriptionPlan.Starter.Key, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, paymentGateway.PlanChangeCalls);
        Assert.Equal(PlanChangeTiming.NextBillingPeriod, paymentGateway.LastPlanChangeTiming);
        Assert.Equal("sub_1", paymentGateway.LastPlanChangeSubscriptionId);
        Assert.Equal(0m, paymentGateway.NextChargedAmount);
        // Still on Growth right now - the whole point of scheduling instead of switching immediately.
        Assert.Equal(SubscriptionPlan.Growth.Key, local.PlanKey);
        Assert.Equal(SubscriptionPlan.Starter.Key, local.PendingPlanKey);
        Assert.Equal(periodEnd, local.PendingPlanEffectiveAt);
        Assert.Equal(SubscriptionPlan.Growth.Key, result.Value!.PlanKey);
        Assert.Equal(SubscriptionPlan.Starter.Key, result.Value.PendingPlanKey);
        Assert.Equal(periodEnd, result.Value.PendingPlanEffectiveAt);
    }

    [Fact]
    public async Task ChangePlanAsync_CanRetargetAnAlreadyScheduledDowngradeToADifferentPlan()
    {
        var (service, user, _, subscriptions, paymentGateway, _, _) = CreateService();
        var now = DateTimeOffset.UtcNow;
        var periodEnd = now.AddMonths(1);
        var local = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Business.Key);
        local.AttachPaddleCustomer("ctm_1");
        local.SyncFromPaddle(SubscriptionPlan.Business.Key, SubscriptionStatus.Active, now, periodEnd, "sub_1", "ctm_1", now);
        local.ScheduleDowngrade(SubscriptionPlan.Growth.Key, periodEnd);
        subscriptions.Add(local);
        paymentGateway.NextChangedSubscription = new PaymentSubscriptionState(
            "ctm_1", "sub_1", SubscriptionPlan.Business.Key, SubscriptionStatus.Active, now, periodEnd, user.OrganizationId);
        paymentGateway.NextPendingEffectiveAt = periodEnd;

        var result = await service.ChangePlanAsync(SubscriptionPlan.Starter.Key, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SubscriptionPlan.Starter.Key, local.PendingPlanKey);
    }

    [Fact]
    public async Task CancelScheduledPlanChangeAsync_ClearsPendingStateAndCancelsItOnPaddle()
    {
        var (service, user, _, subscriptions, paymentGateway, _, _) = CreateService();
        var now = DateTimeOffset.UtcNow;
        var local = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Growth.Key);
        local.AttachPaddleCustomer("ctm_1");
        local.SyncFromPaddle(SubscriptionPlan.Growth.Key, SubscriptionStatus.Active, now, now.AddMonths(1), "sub_1", "ctm_1", now);
        local.ScheduleDowngrade(SubscriptionPlan.Starter.Key, now.AddMonths(1));
        subscriptions.Add(local);

        var result = await service.CancelScheduledPlanChangeAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, paymentGateway.CancelScheduledChangeCalls);
        Assert.Equal("sub_1", paymentGateway.LastCancelScheduledSubscriptionId);
        Assert.Null(local.PendingPlanKey);
        Assert.Null(local.PendingPlanEffectiveAt);
        Assert.Null(result.Value!.PendingPlanKey);
    }

    [Fact]
    public async Task ChangePlanAsync_SendsACongratulationsEmail_OnAnImmediateUpgrade()
    {
        var (service, user, _, subscriptions, paymentGateway, _, _) = CreateServiceWithEmail(out var emailSender, out _, out _);
        var now = DateTimeOffset.UtcNow;
        var local = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Starter.Key);
        local.AttachPaddleCustomer("ctm_1");
        local.SyncFromPaddle(SubscriptionPlan.Starter.Key, SubscriptionStatus.Active, now, now.AddMonths(1), "sub_1", "ctm_1", now);
        subscriptions.Add(local);
        paymentGateway.NextChangedSubscription = new PaymentSubscriptionState(
            "ctm_1", "sub_1", SubscriptionPlan.Growth.Key, SubscriptionStatus.Active, now, now.AddMonths(1), user.OrganizationId);

        await service.ChangePlanAsync(SubscriptionPlan.Growth.Key, CancellationToken.None);

        var sent = Assert.Single(emailSender.Sent);
        Assert.Equal(user.Email, sent.To);
        Assert.Contains(SubscriptionPlan.Growth.Name, sent.Subject);
    }

    [Fact]
    public async Task ChangePlanAsync_SendsAFriendlyNotice_WhenSchedulingADowngrade()
    {
        var (service, user, _, subscriptions, paymentGateway, _, _) = CreateServiceWithEmail(out var emailSender, out _, out _);
        var now = DateTimeOffset.UtcNow;
        var periodEnd = now.AddMonths(1);
        var local = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Growth.Key);
        local.AttachPaddleCustomer("ctm_1");
        local.SyncFromPaddle(SubscriptionPlan.Growth.Key, SubscriptionStatus.Active, now, periodEnd, "sub_1", "ctm_1", now);
        subscriptions.Add(local);
        paymentGateway.NextChangedSubscription = new PaymentSubscriptionState(
            "ctm_1", "sub_1", SubscriptionPlan.Growth.Key, SubscriptionStatus.Active, now, periodEnd, user.OrganizationId);
        paymentGateway.NextPendingEffectiveAt = periodEnd;

        await service.ChangePlanAsync(SubscriptionPlan.Starter.Key, CancellationToken.None);

        var sent = Assert.Single(emailSender.Sent);
        Assert.Equal(user.Email, sent.To);
    }

    [Fact]
    public async Task HandleWebhookAsync_SendsACongratulationsEmailToEveryOwner_OnFirstActivation()
    {
        var (service, user, _, subscriptions, paymentGateway, _, _) = CreateServiceWithEmail(out var emailSender, out var organizations, out var organizationUsers);
        organizations.Add(Organization.CreateSeed(user.OrganizationId, "Acme", "PL", "en", "PLN", "Europe/Warsaw"));
        var owner = new OrganizationUser(user.OrganizationId, "owner@acme.test", "Owner", true);
        owner.Update("owner@acme.test", "Owner", true, [TenebitRoles.Owner]);
        organizationUsers.Users.Add(owner);
        var nonOwner = new OrganizationUser(user.OrganizationId, "employee@acme.test", "Employee", true);
        nonOwner.Update("employee@acme.test", "Employee", true, [TenebitRoles.Employee]);
        organizationUsers.Users.Add(nonOwner);

        var existing = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Free.Key);
        existing.AttachPaddleCustomer("ctm_123");
        subscriptions.Add(existing);
        var periodEnd = DateTimeOffset.UtcNow.AddMonths(1);
        paymentGateway.NextWebhookEvent = new PaymentWebhookEvent(
            "ntf_created_1", "subscription.created", "ctm_123", "sub_123", SubscriptionPlan.Business.Key, SubscriptionStatus.Active, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, periodEnd, null);

        var result = await service.HandleWebhookAsync("{}", "ts=1;h1=fake", CancellationToken.None);

        Assert.True(result.IsSuccess);
        var sent = Assert.Single(emailSender.Sent);
        Assert.Equal("owner@acme.test", sent.To);
        Assert.Contains(SubscriptionPlan.Business.Name, sent.Subject);
    }

    [Fact]
    public async Task HandleWebhookAsync_EmailsWhenAScheduledDowngradeFinallyTakesEffect()
    {
        // The org asked for a smaller plan a month ago; Paddle flips the price at the period end and
        // tells us by webhook. That is the moment they actually move to the smaller plan, so it gets the
        // same warm email as any other plan change - and the pending markers clear.
        var (service, user, _, subscriptions, paymentGateway, _, _) = CreateServiceWithEmail(out var emailSender, out var organizations, out var organizationUsers);
        organizations.Add(Organization.CreateSeed(user.OrganizationId, "Acme", "PL", "en", "PLN", "Europe/Warsaw"));
        var owner = new OrganizationUser(user.OrganizationId, "owner@acme.test", "Owner", true);
        owner.Update("owner@acme.test", "Owner", true, [TenebitRoles.Owner]);
        organizationUsers.Users.Add(owner);

        var now = DateTimeOffset.UtcNow;
        var existing = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Growth.Key);
        existing.SyncFromPaddle(SubscriptionPlan.Growth.Key, SubscriptionStatus.Active, now.AddMonths(-1), now, "sub_123", "ctm_123", now.AddMonths(-1));
        existing.ScheduleDowngrade(SubscriptionPlan.Starter.Key, now);
        subscriptions.Add(existing);
        paymentGateway.NextWebhookEvent = new PaymentWebhookEvent(
            "ntf_phase_1", "subscription.updated", "ctm_123", "sub_123", SubscriptionPlan.Starter.Key, SubscriptionStatus.Active, now, now, now.AddMonths(1), null);

        var result = await service.HandleWebhookAsync("{}", "ts=1;h1=fake", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SubscriptionPlan.Starter.Key, existing.PlanKey);
        Assert.Null(existing.PendingPlanKey);
        var sent = Assert.Single(emailSender.Sent);
        Assert.Equal("owner@acme.test", sent.To);
        Assert.Contains(SubscriptionPlan.Starter.Name, sent.Subject);
    }

    [Fact]
    public async Task HandleWebhookAsync_DoesNotEmail_OnAnOrdinaryRenewal()
    {
        // Only the free-or-cancelled -> paid transition is a "you just subscribed" moment - a routine
        // renewal webhook for an org that was already on a paid plan must stay silent.
        var (service, user, _, subscriptions, paymentGateway, _, _) = CreateServiceWithEmail(out var emailSender, out var organizations, out var organizationUsers);
        organizations.Add(Organization.CreateSeed(user.OrganizationId, "Acme", "PL", "en", "PLN", "Europe/Warsaw"));
        var owner = new OrganizationUser(user.OrganizationId, "owner@acme.test", "Owner", true);
        owner.Update("owner@acme.test", "Owner", true, [TenebitRoles.Owner]);
        organizationUsers.Users.Add(owner);

        var now = DateTimeOffset.UtcNow;
        var existing = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Business.Key);
        existing.SyncFromPaddle(SubscriptionPlan.Business.Key, SubscriptionStatus.Active, now.AddMonths(-1), now, "sub_123", "ctm_123", now.AddMonths(-1));
        subscriptions.Add(existing);
        paymentGateway.NextWebhookEvent = new PaymentWebhookEvent(
            "ntf_renewed_1", "subscription.updated", "ctm_123", "sub_123", SubscriptionPlan.Business.Key, SubscriptionStatus.Active, now, now, now.AddMonths(1), null);

        var result = await service.HandleWebhookAsync("{}", "ts=1;h1=fake", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(emailSender.Sent);
    }

    [Fact]
    public async Task CancelScheduledPlanChangeAsync_Fails_WhenNothingIsPending()
    {
        var (service, user, _, subscriptions, paymentGateway, _, _) = CreateService();
        var now = DateTimeOffset.UtcNow;
        var local = new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Growth.Key);
        local.AttachPaddleCustomer("ctm_1");
        local.SyncFromPaddle(SubscriptionPlan.Growth.Key, SubscriptionStatus.Active, now, now.AddMonths(1), "sub_1", "ctm_1", now);
        subscriptions.Add(local);

        var result = await service.CancelScheduledPlanChangeAsync(CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(0, paymentGateway.CancelScheduledChangeCalls);
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
