using Tenebit.Application.Assets;
using Tenebit.Domain.Assets;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class AssetInspectionServiceTests
{
    private static (AssetInspectionService Service, FakeCurrentUser User, InMemoryAssetInspectionRepository Inspections, InMemoryAssetRepository Assets) CreateService()
    {
        var currentUser = new FakeCurrentUser();
        var inspections = new InMemoryAssetInspectionRepository();
        var assets = new InMemoryAssetRepository();

        var service = new AssetInspectionService(
            inspections,
            assets,
            new InMemoryActivityLogRepository(),
            currentUser,
            new FakeClock(),
            new FakeUnitOfWork());

        return (service, currentUser, inspections, assets);
    }

    private static (AssetInspection Inspection, Asset Asset) AddPendingInspection(FakeCurrentUser user, InMemoryAssetInspectionRepository inspections, InMemoryAssetRepository assets)
    {
        var asset = new Asset(user.OrganizationId, Guid.NewGuid(), "Laptop", "AT-INS-1");
        asset.AssignTo(Guid.NewGuid());
        asset.ReleaseAssignment(AssetStatus.InService);
        assets.Add(asset);
        var inspection = new AssetInspection(user.OrganizationId, asset.Id, Guid.NewGuid(), DateTimeOffset.UtcNow, "tester");
        inspections.Add(inspection);
        return (inspection, asset);
    }

    [Fact]
    public async Task CompleteAsync_ReadyForReuse_SetsAssetInStock()
    {
        var (service, user, inspections, assets) = CreateService();
        var (inspection, asset) = AddPendingInspection(user, inspections, assets);

        var result = await service.CompleteAsync(inspection.Id, new CompleteAssetInspectionRequest(InspectionOutcome.ReadyForReuse, true, true, true, true, null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AssetStatus.InStock, asset.Status);
    }

    [Fact]
    public async Task CompleteAsync_Damaged_SetsAssetDamaged()
    {
        var (service, user, inspections, assets) = CreateService();
        var (inspection, asset) = AddPendingInspection(user, inspections, assets);

        var result = await service.CompleteAsync(inspection.Id, new CompleteAssetInspectionRequest(InspectionOutcome.Damaged, true, true, true, false, "Pęknięta obudowa", null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AssetStatus.Damaged, asset.Status);
    }

    [Fact]
    public async Task CompleteAsync_Retired_SetsAssetRetired()
    {
        var (service, user, inspections, assets) = CreateService();
        var (inspection, asset) = AddPendingInspection(user, inspections, assets);

        var result = await service.CompleteAsync(inspection.Id, new CompleteAssetInspectionRequest(InspectionOutcome.Retired, true, true, true, true, null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AssetStatus.Retired, asset.Status);
    }

    [Fact]
    public async Task CompleteAsync_Disposed_SetsAssetDisposed()
    {
        var (service, user, inspections, assets) = CreateService();
        var (inspection, asset) = AddPendingInspection(user, inspections, assets);

        var result = await service.CompleteAsync(inspection.Id, new CompleteAssetInspectionRequest(InspectionOutcome.Disposed, true, true, true, true, null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AssetStatus.Disposed, asset.Status);
    }

    [Fact]
    public async Task CompleteAsync_IsIdempotent_SecondCallReturnsSameOutcomeWithoutThrowing()
    {
        var (service, user, inspections, assets) = CreateService();
        var (inspection, asset) = AddPendingInspection(user, inspections, assets);

        await service.CompleteAsync(inspection.Id, new CompleteAssetInspectionRequest(InspectionOutcome.ReadyForReuse, true, true, true, true, null, null), CancellationToken.None);
        var second = await service.CompleteAsync(inspection.Id, new CompleteAssetInspectionRequest(InspectionOutcome.Damaged, false, false, false, false, "should be ignored", null), CancellationToken.None);

        Assert.True(second.IsSuccess);
        Assert.Equal(InspectionOutcome.ReadyForReuse, second.Value!.Outcome);
        Assert.Equal(AssetStatus.InStock, asset.Status);
    }

    [Fact]
    public async Task CompleteAsync_RejectsRoleWithoutAssetAccess()
    {
        var (service, user, inspections, assets) = CreateService();
        var (inspection, _) = AddPendingInspection(user, inspections, assets);
        user.Roles = ["employee"];

        var result = await service.CompleteAsync(inspection.Id, new CompleteAssetInspectionRequest(InspectionOutcome.ReadyForReuse, true, true, true, true, null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task GetPendingForAssetAsync_ReturnsNotFound_WhenNoneExists()
    {
        var (service, _, _, _) = CreateService();

        var result = await service.GetPendingForAssetAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error!.Code);
    }
}
