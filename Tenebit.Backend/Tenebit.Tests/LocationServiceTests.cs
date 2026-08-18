using Tenebit.Application.Assets;
using Tenebit.Application.Common;
using Tenebit.Domain.Assets;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public sealed class LocationServiceTests
{
    private static (LocationService Service, FakeCurrentUser User, InMemoryLocationRepository Locations, InMemoryAssetRepository Assets, InMemoryPersonRepository People) CreateService()
    {
        var user = new FakeCurrentUser();
        var locations = new InMemoryLocationRepository();
        var assets = new InMemoryAssetRepository();
        var people = new InMemoryPersonRepository();
        var teams = new InMemoryTeamRepository();
        return (new LocationService(locations, assets, people, user, new FakeUnitOfWork(), new ManagerScopeService(people, teams)), user, locations, assets, people);
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicateSiblingName_CaseInsensitively()
    {
        var (service, user, locations, _, _) = CreateService();
        locations.Add(new Location(user.OrganizationId, "Biuro", "Room", null));

        var result = await service.CreateAsync(new CreateLocationRequest("  BIURO  ", "Room", null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Single(locations.Locations);
    }

    [Fact]
    public async Task RenameParent_PreservesStableLocationId_AndRefreshesCachedPath()
    {
        var (service, user, locations, assets, _) = CreateService();
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
        var (service, user, locations, assets, _) = CreateService();
        var room = new Location(user.OrganizationId, "Magazyn", "Room", null);
        locations.Add(room);
        var asset = new Asset(user.OrganizationId, Guid.NewGuid(), "Laptop", "AT-002");
        asset.SetLocation(room.Id, "old/stale/path");
        assets.Add(asset);

        var result = await service.DeleteAsync(room.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Single(locations.Locations);
    }
}
