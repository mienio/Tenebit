using Tenebit.Domain.Assets;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class AssetLocationFilterTests
{
    private static (FakeCurrentUser User, InMemoryAssetRepository Assets) Create(FakeCurrentUser? user = null)
    {
        var currentUser = user ?? new FakeCurrentUser();
        return (currentUser, new InMemoryAssetRepository());
    }

    private static Asset AddAsset(FakeCurrentUser user, InMemoryAssetRepository assets, string? location)
    {
        var asset = new Asset(user.OrganizationId, Guid.NewGuid(), "Asset", $"AT-{assets.Assets.Count + 1}");
        if (location is not null) asset.UpdateCore("Asset", asset.AssetTag, null, null, location, null, null, null, null, null, null, null);
        assets.Add(asset);
        return asset;
    }

    [Fact]
    public async Task ListAsync_FilterByParentLocation_IncludesChildren()
    {
        var (user, assets) = Create();
        AddAsset(user, assets, "Budynek A");
        AddAsset(user, assets, "Budynek A / Pietro 1");
        AddAsset(user, assets, "Budynek A / Pietro 1 / Pokoj 204");
        AddAsset(user, assets, "Budynek B");

        var result = await assets.ListAsync(user.OrganizationId, null, null, "Budynek A", CancellationToken.None);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task ListAsync_FilterByParentLocation_DoesNotMatchSimilarPrefix()
    {
        var (user, assets) = Create();
        AddAsset(user, assets, "Budynek A");
        AddAsset(user, assets, "Budynek AB");
        AddAsset(user, assets, "Budynek AB / Pietro 1");

        var result = await assets.ListAsync(user.OrganizationId, null, null, "Budynek A", CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("Budynek A", result[0].Location);
    }

    [Fact]
    public async Task ListPagedAsync_FilterByMidLevelLocation_IncludesDescendants()
    {
        var (user, assets) = Create();
        AddAsset(user, assets, "Budynek A / Pietro 1");
        AddAsset(user, assets, "Budynek A / Pietro 1 / Pokoj 204");
        AddAsset(user, assets, "Budynek A / Pietro 2");

        var result = await assets.ListPagedAsync(user.OrganizationId, null, null, "Budynek A / Pietro 1", null, null, false, null, null, null, false, 1, 25, CancellationToken.None);

        Assert.Equal(2, result.Total);
    }
}
