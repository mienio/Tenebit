using Tenebit.Domain.Assets;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

// Regression coverage for BUG-002: asset search must be case-insensitive.
public class AssetSearchFilterTests
{
    private static (FakeCurrentUser User, InMemoryAssetRepository Assets) Create()
    {
        var user = new FakeCurrentUser();
        return (user, new InMemoryAssetRepository());
    }

    private static Asset AddAsset(FakeCurrentUser user, InMemoryAssetRepository assets, string name)
    {
        var asset = new Asset(user.OrganizationId, Guid.NewGuid(), name, $"AT-{assets.Assets.Count + 1}");
        assets.Add(asset);
        return asset;
    }

    [Theory]
    [InlineData("QA Test")]
    [InlineData("qa test")]
    [InlineData("Qa TeSt")]
    public async Task ListAsync_SearchIsCaseInsensitive(string searchTerm)
    {
        var (user, assets) = Create();
        AddAsset(user, assets, "QA Test Laptop ĄĆŹ");

        var result = await assets.ListAsync(user.OrganizationId, searchTerm, null, null, CancellationToken.None);

        Assert.Single(result);
    }

    [Theory]
    [InlineData("QA Test")]
    [InlineData("qa test")]
    public async Task ListPagedAsync_SearchIsCaseInsensitive(string searchTerm)
    {
        var (user, assets) = Create();
        AddAsset(user, assets, "QA Test Laptop ĄĆŹ");

        var result = await assets.ListPagedAsync(user.OrganizationId, searchTerm, null, null, null, null, false, null, null, null, false, 1, 25, CancellationToken.None);

        Assert.Equal(1, result.Total);
    }
}
