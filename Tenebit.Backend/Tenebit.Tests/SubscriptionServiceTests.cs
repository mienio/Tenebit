using Tenebit.Application.Subscriptions;
using Tenebit.Domain.Assets;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class SubscriptionServiceTests
{
    private static (SubscriptionService Service, FakeCurrentUser User, InMemoryAssetRepository Assets, InMemorySubscriptionRepository Subscriptions) CreateService()
    {
        var currentUser = new FakeCurrentUser();
        var assets = new InMemoryAssetRepository();
        var subscriptions = new InMemorySubscriptionRepository();
        var service = new SubscriptionService(subscriptions, assets, new InMemoryActivityLogRepository(), currentUser, new FakeClock(), new FakeUnitOfWork());
        return (service, currentUser, assets, subscriptions);
    }

    [Fact]
    public async Task GetCurrentAsync_CreatesDefaultFreeSubscriptionWhenNoneExists()
    {
        var (service, _, _, subscriptions) = CreateService();

        var result = await service.GetCurrentAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("free", result.Value!.PlanKey);
        Assert.Single(subscriptions.Subscriptions);
    }

    [Fact]
    public async Task UpgradeAsync_RejectsNonOwnerRole()
    {
        var (service, user, _, _) = CreateService();
        user.Roles = ["employee"];

        var result = await service.UpgradeAsync("pro", CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task UpgradeAsync_RejectsUnknownPlanKey()
    {
        var (service, _, _, _) = CreateService();

        var result = await service.UpgradeAsync("enterprise-does-not-exist", CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task UpgradeAsync_SwitchesPlanForOwner()
    {
        var (service, _, _, _) = CreateService();

        var result = await service.UpgradeAsync("pro", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("pro", result.Value!.PlanKey);
    }

    [Fact]
    public async Task CanAddAssetAsync_ReturnsFalseAtFreePlanLimit()
    {
        var (service, user, assets, _) = CreateService();
        for (var i = 0; i < 10; i++)
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
        var (service, user, assets, _) = CreateService();
        for (var i = 0; i < 3; i++)
        {
            assets.Add(new Asset(user.OrganizationId, Guid.NewGuid(), $"Asset {i}", $"AT-{i:000}"));
        }

        var result = await service.CanAddAssetAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
    }
}
