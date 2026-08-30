using Microsoft.Extensions.Logging.Abstractions;
using Tenebit.Application.Assets;
using Tenebit.Application.Common;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Organizations;
using Tenebit.Domain.Subscriptions;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

/// <summary>
/// A batch is either created whole or not at all. The half-written case is the expensive one in the
/// field: the labels for the whole run are already printed, so a partial batch means working out by
/// hand which tags exist before anything can be stuck on a box.
/// </summary>
public class AssetBatchCreateTests
{
    private static (AssetService Service, FakeCurrentUser User, InMemoryAssetRepository Assets, InMemoryAssetCategoryRepository Categories, InMemorySubscriptionRepository Subscriptions) CreateService()
    {
        var currentUser = new FakeCurrentUser();
        var organizations = new InMemoryOrganizationRepository();
        var organization = new Organization("Acme", "PL", "pl", "PLN", "UTC");
        organizations.Add(organization);
        currentUser.OrganizationId = organization.Id;

        var assets = new InMemoryAssetRepository();
        var categories = new InMemoryAssetCategoryRepository();
        var subscriptions = new InMemorySubscriptionRepository();
        var people = new InMemoryPersonRepository();
        var teams = new InMemoryTeamRepository();

        var service = new AssetService(
            assets,
            new InMemoryPublicReportThrottleRepository(),
            new InMemoryMaintenanceScheduleRepository(),
            categories,
            people,
            teams,
            new InMemoryActivityLogRepository(),
            subscriptions,
            organizations,
            new InMemoryOrganizationUserRepository(),
            currentUser,
            new FakeClock(),
            new FakeUnitOfWork(),
            new FakeQrCodeGenerator(),
            new FakeAppLinkBuilder(),
            new FakeEmailSender(),
            NullLogger<AssetService>.Instance,
            new FakeFieldEncryptor(),
            new ManagerScopeService(people, teams),
            new LocationReferenceResolver(new InMemoryLocationRepository()));

        return (service, currentUser, assets, categories, subscriptions);
    }

    private static AssetCategory AddCategory(FakeCurrentUser user, InMemoryAssetCategoryRepository categories)
    {
        var category = new AssetCategory(user.OrganizationId, "Laptopy", AssetCategoryType.Physical, null);
        categories.Add(category);
        return category;
    }

    private static CreateAssetBatchRequest BuildRequest(
        Guid categoryId,
        int quantity = 3,
        string prefix = "LAP-",
        int start = 14,
        int padding = 4,
        IReadOnlyList<string>? serials = null) =>
        new("Dell Latitude 5450", categoryId, quantity, prefix, start, padding, serials, null, null, null, null, null, null, null, null, null);

    [Fact]
    public async Task CreateBatchAsync_NumbersTagsFromTheStartNumber()
    {
        var (service, user, assets, categories, _) = CreateService();
        var category = AddCategory(user, categories);

        var result = await service.CreateBatchAsync(BuildRequest(category.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.Created);
        Assert.Equal(["LAP-0014", "LAP-0015", "LAP-0016"], result.Value.Assets.Select(asset => asset.AssetTag));
        Assert.Equal(3, assets.Assets.Count(asset => asset.OrganizationId == user.OrganizationId));
    }

    [Fact]
    public async Task CreateBatchAsync_AssignsSerialNumbersInOrderAndLeavesTheRestEmpty()
    {
        var (service, user, _, categories, _) = CreateService();
        var category = AddCategory(user, categories);

        var result = await service.CreateBatchAsync(
            BuildRequest(category.Id, quantity: 3, serials: ["SN-A", "SN-B"]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["SN-A", "SN-B", null], result.Value!.Assets.Select(asset => asset.SerialNumber));
    }

    [Fact]
    public async Task CreateBatchAsync_RejectsWholeBatchWhenOneTagIsTaken()
    {
        var (service, user, assets, categories, _) = CreateService();
        var category = AddCategory(user, categories);
        var existing = new Asset(user.OrganizationId, category.Id, "Stary laptop", "LAP-0015");
        assets.Add(existing);

        var result = await service.CreateBatchAsync(BuildRequest(category.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("LAP-0015", result.Error!.Message);
        Assert.Single(assets.Assets.Where(asset => asset.OrganizationId == user.OrganizationId));
    }

    [Fact]
    public async Task CreateBatchAsync_RejectsWholeBatchWhenItWouldCrossThePlanLimit()
    {
        var (service, user, assets, categories, subscriptions) = CreateService();
        var category = AddCategory(user, categories);
        subscriptions.Add(new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Free.Key));
        for (var i = 0; i < 8; i++)
        {
            assets.Add(new Asset(user.OrganizationId, category.Id, $"Laptop {i}", $"OLD-{i}"));
        }

        // Free allows 10 assets, 8 exist - a run of 5 does not fit, and none of it may land.
        var result = await service.CreateBatchAsync(BuildRequest(category.Id, quantity: 5), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(8, assets.Assets.Count(asset => asset.OrganizationId == user.OrganizationId));
    }

    [Fact]
    public async Task CreateBatchAsync_RejectsMoreSerialNumbersThanUnits()
    {
        var (service, user, _, categories, _) = CreateService();
        var category = AddCategory(user, categories);

        var result = await service.CreateBatchAsync(
            BuildRequest(category.Id, quantity: 2, serials: ["SN-A", "SN-B", "SN-C"]),
            CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task CreateBatchAsync_RejectsUserWithoutAssetManagementRole()
    {
        var (service, user, _, categories, _) = CreateService();
        var category = AddCategory(user, categories);
        user.Roles = ["employee"];

        var result = await service.CreateBatchAsync(BuildRequest(category.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
    }
}
