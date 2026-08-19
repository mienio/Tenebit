using Tenebit.Application.Assets;
using Tenebit.Domain.Assets;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class AssetCategoryServiceTests
{
    private static (AssetCategoryService Service, FakeCurrentUser User, InMemoryAssetCategoryRepository Categories, InMemoryActivityLogRepository Activity) CreateService()
    {
        var currentUser = new FakeCurrentUser();
        var categories = new InMemoryAssetCategoryRepository();
        var activity = new InMemoryActivityLogRepository();

        var service = new AssetCategoryService(
            categories,
            activity,
            currentUser,
            new FakeClock(),
            new FakeUnitOfWork());

        return (service, currentUser, categories, activity);
    }

    [Fact]
    public async Task CreateAsync_DefaultsToDirectToStockAndReuse()
    {
        var (service, _, _, _) = CreateService();

        var result = await service.CreateAsync(new CreateAssetCategoryRequest("Klawiatury", AssetCategoryType.Physical, null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ReturnHandlingMode.DirectToStock, result.Value!.ReturnHandlingMode);
        Assert.Equal(PostReturnDisposition.Reuse, result.Value!.PostReturnDisposition);
    }

    [Fact]
    public async Task UpdateReturnPolicyAsync_UpdatesFieldsAndWritesActivityLog()
    {
        var (service, user, categories, activity) = CreateService();
        var category = new AssetCategory(user.OrganizationId, "Laptopy", AssetCategoryType.Physical, null);
        categories.Add(category);

        var request = new UpdateAssetCategoryReturnPolicyRequest(ReturnHandlingMode.InspectionRequired, PostReturnDisposition.ReturnToVendor, "SprawdĹş obudowÄ™ i akcesoria", PhotoRequirement.Required, PhotoRequirement.Optional);
        var result = await service.UpdateReturnPolicyAsync(category.Id, request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ReturnHandlingMode.InspectionRequired, result.Value!.ReturnHandlingMode);
        Assert.Equal(PostReturnDisposition.ReturnToVendor, result.Value!.PostReturnDisposition);
        Assert.Equal("SprawdĹş obudowÄ™ i akcesoria", result.Value!.ReturnChecklistTemplate);
        Assert.Equal(PhotoRequirement.Required, result.Value!.PhotoOnIssue);
        Assert.Equal(PhotoRequirement.Optional, result.Value!.PhotoOnReturn);
        Assert.Contains(activity.Logs, x => x.Action == "asset_category.return_policy_updated");
    }

    [Fact]
    public async Task UpdateReturnPolicyAsync_RejectsNonAdminRole()
    {
        var (service, user, categories, _) = CreateService();
        user.Roles = ["employee"];
        var category = new AssetCategory(user.OrganizationId, "Laptopy", AssetCategoryType.Physical, null);
        categories.Add(category);

        var request = new UpdateAssetCategoryReturnPolicyRequest(ReturnHandlingMode.InspectionRequired, PostReturnDisposition.Reuse, null, PhotoRequirement.Disabled, PhotoRequirement.Disabled);
        var result = await service.UpdateReturnPolicyAsync(category.Id, request, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task UpdateReturnPolicyAsync_ReturnsNotFound_ForUnknownCategory()
    {
        var (service, _, _, _) = CreateService();

        var request = new UpdateAssetCategoryReturnPolicyRequest(ReturnHandlingMode.InspectionRequired, PostReturnDisposition.Reuse, null, PhotoRequirement.Disabled, PhotoRequirement.Disabled);
        var result = await service.UpdateReturnPolicyAsync(Guid.NewGuid(), request, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("ASSET_CATEGORY_NOT_FOUND", result.Error!.Code);
    }

    [Fact]
    public void StarterAssetCategories_SeedsInspectionRequiredForLaptopsPhonesVehiclesOnly()
    {
        var categories = StarterAssetCategories.Create(Guid.NewGuid());

        var inspectionRequired = categories.Where(x => x.ReturnHandlingMode == ReturnHandlingMode.InspectionRequired).Select(x => x.Name).OrderBy(x => x).ToList();

        Assert.Equal(new[] { "Laptopy", "Pojazdy", "Telefony" }, inspectionRequired);
        Assert.All(categories, x => Assert.Equal(PostReturnDisposition.Reuse, x.PostReturnDisposition));
    }
}
