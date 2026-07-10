using Microsoft.Extensions.Logging.Abstractions;
using Tenebit.Application.Assets;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Subscriptions;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class AssetServiceTests
{
    private static (AssetService Service, FakeCurrentUser User, InMemoryAssetRepository Assets, InMemoryAssetCategoryRepository Categories, InMemorySubscriptionRepository Subscriptions) CreateService()
    {
        var currentUser = new FakeCurrentUser();
        var assets = new InMemoryAssetRepository();
        var categories = new InMemoryAssetCategoryRepository();
        var subscriptions = new InMemorySubscriptionRepository();

        var service = new AssetService(
            assets,
            categories,
            new InMemoryPersonRepository(),
            new InMemoryTeamRepository(),
            new InMemoryActivityLogRepository(),
            subscriptions,
            new InMemoryOrganizationRepository(),
            new InMemoryOrganizationUserRepository(),
            currentUser,
            new FakeClock(),
            new FakeUnitOfWork(),
            new FakeQrCodeGenerator(),
            new FakeAppLinkBuilder(),
            new FakeEmailSender(),
            NullLogger<AssetService>.Instance);

        return (service, currentUser, assets, categories, subscriptions);
    }

    private static AssetCategory AddCategory(FakeCurrentUser user, InMemoryAssetCategoryRepository categories)
    {
        var category = new AssetCategory(user.OrganizationId, "Laptopy", AssetCategoryType.Physical, "Sprzęt komputerowy");
        categories.Add(category);
        return category;
    }

    private static CreateAssetRequest BuildRequest(Guid categoryId, string tag = "AT-001") =>
        new("Laptop Dell", tag, null, categoryId, null, null, null, null, null, null, null, null, null);

    [Fact]
    public async Task CreateAsync_RejectsUserWithoutAssetManagementRole()
    {
        var (service, user, _, categories, _) = CreateService();
        user.Roles = ["employee"];
        var category = AddCategory(user, categories);

        var result = await service.CreateAsync(BuildRequest(category.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task CreateAsync_RejectsUnknownCategory()
    {
        var (service, _, _, _, _) = CreateService();

        var result = await service.CreateAsync(BuildRequest(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicateAssetTag()
    {
        var (service, user, _, categories, _) = CreateService();
        var category = AddCategory(user, categories);

        var first = await service.CreateAsync(BuildRequest(category.Id, "AT-DUP"), CancellationToken.None);
        Assert.True(first.IsSuccess);

        var second = await service.CreateAsync(BuildRequest(category.Id, "AT-DUP"), CancellationToken.None);

        Assert.True(second.IsFailure);
    }

    [Fact]
    public async Task CreateAsync_SucceedsUnderFreePlanLimit()
    {
        var (service, user, _, categories, _) = CreateService();
        var category = AddCategory(user, categories);

        var result = await service.CreateAsync(BuildRequest(category.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Laptop Dell", result.Value!.Name);
    }

    [Fact]
    public async Task CreateAsync_RejectsWhenAtSubscriptionAssetLimit()
    {
        var (service, user, assets, categories, subscriptions) = CreateService();
        var category = AddCategory(user, categories);
        subscriptions.Add(new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Free.Key));

        for (var i = 0; i < SubscriptionPlan.Free.AssetLimit; i++)
        {
            assets.Add(new Asset(user.OrganizationId, category.Id, $"Asset {i}", $"AT-{i:0000}"));
        }

        var result = await service.CreateAsync(BuildRequest(category.Id, "AT-OVER-LIMIT"), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task DeleteAsync_RejectsAssetOperatorRole()
    {
        var (service, user, assets, categories, _) = CreateService();
        var category = AddCategory(user, categories);
        var created = await service.CreateAsync(BuildRequest(category.Id), CancellationToken.None);
        user.Roles = ["asset_operator"];

        var result = await service.DeleteAsync(created.Value!.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task DeleteAsync_SucceedsForOwner()
    {
        var (service, user, _, categories, _) = CreateService();
        var category = AddCategory(user, categories);
        var created = await service.CreateAsync(BuildRequest(category.Id), CancellationToken.None);

        var result = await service.DeleteAsync(created.Value!.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var afterDelete = await service.GetAsync(created.Value!.Id, CancellationToken.None);
        Assert.True(afterDelete.IsFailure);
    }
}
