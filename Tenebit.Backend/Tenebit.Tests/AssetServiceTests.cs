using Microsoft.Extensions.Logging.Abstractions;
using Tenebit.Application.Assets;
using Tenebit.Application.Common;
using Tenebit.Domain.Assets;
using Tenebit.Domain.People;
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
        var people = new InMemoryPersonRepository();
        var teams = new InMemoryTeamRepository();

        var service = new AssetService(
            assets,
            categories,
            people,
            teams,
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
            NullLogger<AssetService>.Instance,
            new FakeFieldEncryptor(),
            new ManagerScopeService(people, teams), new LocationReferenceResolver(new InMemoryLocationRepository()));

        return (service, currentUser, assets, categories, subscriptions);
    }

    private static (AssetService Service, FakeCurrentUser User, InMemoryAssetCategoryRepository Categories, InMemoryTeamRepository Teams) CreateServiceWithTeams()
    {
        var currentUser = new FakeCurrentUser();
        var assets = new InMemoryAssetRepository();
        var categories = new InMemoryAssetCategoryRepository();
        var teams = new InMemoryTeamRepository();
        var people = new InMemoryPersonRepository();

        var service = new AssetService(
            assets,
            categories,
            people,
            teams,
            new InMemoryActivityLogRepository(),
            new InMemorySubscriptionRepository(),
            new InMemoryOrganizationRepository(),
            new InMemoryOrganizationUserRepository(),
            currentUser,
            new FakeClock(),
            new FakeUnitOfWork(),
            new FakeQrCodeGenerator(),
            new FakeAppLinkBuilder(),
            new FakeEmailSender(),
            NullLogger<AssetService>.Instance,
            new FakeFieldEncryptor(),
            new ManagerScopeService(people, teams), new LocationReferenceResolver(new InMemoryLocationRepository()));

        return (service, currentUser, categories, teams);
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
    public async Task CreateAsync_RejectsCrossOrganizationTeamId()
    {
        var (service, user, categories, teams) = CreateServiceWithTeams();
        var category = AddCategory(user, categories);
        var otherOrgTeam = new Team(Guid.NewGuid(), "Inny zespół", null, null);
        teams.Add(otherOrgTeam);

        var request = new CreateAssetRequest("Laptop Dell", "AT-001", null, category.Id, null, null, null, null, null, null, null, otherOrgTeam.Id, null);
        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

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

    [Fact]
    public async Task CreateAsync_EncryptsSensitiveFieldAtRest_AndRevealReturnsPlaintext()
    {
        var (service, user, assets, categories, _) = CreateService();
        var category = AddCategory(user, categories);
        category.ReplaceFieldDefinitions([("licenseKey", "Klucz licencji", AssetFieldType.Sensitive, null, false)]);

        var request = BuildRequest(category.Id) with { CustomFields = new Dictionary<string, string> { ["licenseKey"] = "SECRET-1234" } };
        var created = await service.CreateAsync(request, CancellationToken.None);
        Assert.True(created.IsSuccess);

        var stored = assets.Assets.Single(x => x.Id == created.Value!.Id).FieldValues.Single(x => x.FieldKey == "licenseKey").Value;
        Assert.NotEqual("SECRET-1234", stored);

        var revealed = await service.RevealSensitiveFieldAsync(created.Value!.Id, "licenseKey", CancellationToken.None);
        Assert.True(revealed.IsSuccess);
        Assert.Equal("SECRET-1234", revealed.Value);
    }

    [Fact]
    public async Task UpdateAsync_PreservesSensitiveFieldWhenMaskResubmitted()
    {
        var (service, user, _, categories, _) = CreateService();
        var category = AddCategory(user, categories);
        category.ReplaceFieldDefinitions([("licenseKey", "Klucz licencji", AssetFieldType.Sensitive, null, false)]);

        var createRequest = BuildRequest(category.Id) with { CustomFields = new Dictionary<string, string> { ["licenseKey"] = "SECRET-1234" } };
        var created = await service.CreateAsync(createRequest, CancellationToken.None);

        var updateRequest = new UpdateAssetRequest("Laptop Dell", "AT-001", null, category.Id, AssetStatus.InStock, null, null, null, null, null, null, null, null, new Dictionary<string, string> { ["licenseKey"] = "••••••••" });
        var updated = await service.UpdateAsync(created.Value!.Id, updateRequest, CancellationToken.None);
        Assert.True(updated.IsSuccess);

        var revealed = await service.RevealSensitiveFieldAsync(created.Value!.Id, "licenseKey", CancellationToken.None);
        Assert.True(revealed.IsSuccess);
        Assert.Equal("SECRET-1234", revealed.Value);
    }

    [Fact]
    public async Task ReportPublicIssueAsync_BlocksRepeatReportFromSameIpWithinCooldown()
    {
        var (service, user, _, categories, _) = CreateService();
        var category = AddCategory(user, categories);
        var created = await service.CreateAsync(BuildRequest(category.Id), CancellationToken.None);
        var organizationId = user.OrganizationId;
        var assetId = created.Value!.Id;

        var first = await service.ReportPublicIssueAsync(organizationId, assetId, new ReportAssetIssueRequest("Zepsuty ekran"), CancellationToken.None);
        var second = await service.ReportPublicIssueAsync(organizationId, assetId, new ReportAssetIssueRequest("Zepsuty ekran"), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsFailure);
        Assert.Equal("ASSET_REPORT_RATE_LIMITED", second.Error!.Code);
    }

    [Fact]
    public async Task ReportPublicIssueAsync_AllowsReportFromDifferentIpDuringCooldown()
    {
        var (service, user, _, categories, _) = CreateService();
        var category = AddCategory(user, categories);
        var created = await service.CreateAsync(BuildRequest(category.Id), CancellationToken.None);
        var organizationId = user.OrganizationId;
        var assetId = created.Value!.Id;

        user.IpAddress = "10.0.0.1";
        var first = await service.ReportPublicIssueAsync(organizationId, assetId, new ReportAssetIssueRequest("Zepsuty ekran"), CancellationToken.None);
        user.IpAddress = "10.0.0.2";
        var second = await service.ReportPublicIssueAsync(organizationId, assetId, new ReportAssetIssueRequest("Zepsuty ekran"), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
    }
}
