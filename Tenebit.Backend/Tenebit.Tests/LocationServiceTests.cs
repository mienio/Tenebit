using Tenebit.Application.Assets;
using Tenebit.Application.Common;
using Tenebit.Domain.Assets;
using Tenebit.Domain.People;
using Tenebit.Domain.Subscriptions;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public sealed class LocationServiceTests
{
    private static (LocationService Service, FakeCurrentUser User, InMemoryLocationRepository Locations, InMemoryAssetRepository Assets, InMemoryPersonRepository People, InMemoryAssetCategoryRepository Categories, InMemorySubscriptionRepository Subscriptions) CreateService()
    {
        var user = new FakeCurrentUser();
        var locations = new InMemoryLocationRepository();
        var assets = new InMemoryAssetRepository();
        var people = new InMemoryPersonRepository();
        var teams = new InMemoryTeamRepository();
        var categories = new InMemoryAssetCategoryRepository();
        var subscriptions = new InMemorySubscriptionRepository();
        return (new LocationService(locations, assets, people, categories, user, new FakeUnitOfWork(), new ManagerScopeService(people, teams), subscriptions), user, locations, assets, people, categories, subscriptions);
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicateSiblingName_CaseInsensitively()
    {
        var (service, user, locations, _, _, _, _) = CreateService();
        locations.Add(new Location(user.OrganizationId, "Biuro", "Room", null));

        var result = await service.CreateAsync(new CreateLocationRequest("  BIURO  ", "Room", null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Single(locations.Locations);
    }

    [Fact]
    public async Task RenameParent_PreservesStableLocationId_AndRefreshesCachedPath()
    {
        var (service, user, locations, assets, _, _, _) = CreateService();
        var root = new Location(user.OrganizationId, "Biuro", "Building", null);
        var room = new Location(user.OrganizationId, "Pokoj 1", "Room", root.Id);
        locations.Add(root);
        locations.Add(room);

        var asset = new Asset(user.OrganizationId, Guid.NewGuid(), "Laptop", "AT-001");
        asset.SetLocation(room.Id, "Biuro / Pokoj 1");
        assets.Add(asset);

        var result = await service.UpdateAsync(root.Id, new UpdateLocationRequest("Centrala", "Building", null, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(room.Id, asset.LocationId);
        Assert.Equal("Centrala / Pokoj 1", asset.Location);
    }

    [Fact]
    public async Task DeleteAsync_BlocksAssignedAsset_ByLocationId_EvenIfCachedPathIsStale()
    {
        var (service, user, locations, assets, _, _, _) = CreateService();
        var room = new Location(user.OrganizationId, "Magazyn", "Room", null);
        locations.Add(room);
        var asset = new Asset(user.OrganizationId, Guid.NewGuid(), "Laptop", "AT-002");
        asset.SetLocation(room.Id, "old/stale/path");
        assets.Add(asset);

        var result = await service.DeleteAsync(room.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Single(locations.Locations);
    }

    [Fact]
    public async Task CreateAsync_RejectsWhenAtSubscriptionResourceLimit()
    {
        var (service, user, locations, _, _, _, subscriptions) = CreateService();
        subscriptions.Add(new OrganizationSubscription(user.OrganizationId, SubscriptionPlan.Free.Key));

        for (var i = 0; i < SubscriptionPlan.Free.AssetLimit; i++)
        {
            locations.Add(new Location(user.OrganizationId, $"Lokalizacja {i}", "Room", null));
        }

        var result = await service.CreateAsync(new CreateLocationRequest("Lokalizacja nadmiarowa", "Room", null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Limit lokalizacji przekroczony", result.Error!.Message);
    }

    [Fact]
    public async Task GetInventoryAsync_ResolvesRealCategoryAndAssignedPersonName_InsteadOfLeavingThemBlank()
    {
        var (service, user, locations, assets, people, categories, _) = CreateService();
        var room = new Location(user.OrganizationId, "Serwerownia", "Room", null);
        locations.Add(room);

        var category = new AssetCategory(user.OrganizationId, "Monitory", AssetCategoryType.Physical, null, "monitor");
        categories.Add(category);

        var person = new Person(user.OrganizationId, "Jan", "Kowalski", "jan.kowalski@example.com");
        people.People.Add(person);

        var asset = new Asset(user.OrganizationId, category.Id, "Dell UltraSharp 27\"", "AT-100");
        asset.SetLocation(room.Id, "Serwerownia");
        asset.AssignTo(person.Id);
        assets.Add(asset);

        var result = await service.GetInventoryAsync(room.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value!.Assets);
        Assert.Equal(category.Id, item.CategoryId);
        Assert.Equal("Monitory", item.CategoryName);
        Assert.Equal("Jan Kowalski", item.AssignedPersonName);
    }
}
