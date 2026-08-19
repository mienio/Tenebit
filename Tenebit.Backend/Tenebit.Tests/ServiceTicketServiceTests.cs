using Tenebit.Application.Assets;
using Tenebit.Domain.Assets;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class ServiceTicketServiceTests
{
    private static (ServiceTicketService Service, FakeCurrentUser User, InMemoryServiceTicketRepository Tickets, InMemoryAssetRepository Assets, InMemoryAssetInspectionRepository Inspections) CreateService()
    {
        var currentUser = new FakeCurrentUser();
        var tickets = new InMemoryServiceTicketRepository();
        var assets = new InMemoryAssetRepository();
        var inspections = new InMemoryAssetInspectionRepository();

        var service = new ServiceTicketService(
            tickets,
            assets,
            inspections,
            new InMemoryActivityLogRepository(),
            currentUser,
            new FakeClock(),
            new FakeUnitOfWork(),
            TestAuthorization.Asset(assets, currentUser));

        return (service, currentUser, tickets, assets, inspections);
    }

    private static Asset AddInStockAsset(FakeCurrentUser user, InMemoryAssetRepository assets) =>
        new(user.OrganizationId, Guid.NewGuid(), "Laptop", "AT-ST-1");

    [Fact]
    public async Task OpenAsync_SetsAssetInService()
    {
        var (service, user, tickets, assets, _) = CreateService();
        var asset = AddInStockAsset(user, assets);
        assets.Add(asset);

        var result = await service.OpenAsync(new OpenServiceTicketRequest(asset.Id, null, "Vendor X", "opis", null, null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AssetStatus.InService, asset.Status);
        Assert.Single(tickets.Tickets);
    }

    [Fact]
    public async Task CompleteAsync_ValidResultStatus_UpdatesAssetAndTicket()
    {
        var (service, user, tickets, assets, _) = CreateService();
        var asset = AddInStockAsset(user, assets);
        assets.Add(asset);

        var opened = await service.OpenAsync(new OpenServiceTicketRequest(asset.Id, null, "Vendor X", "opis", null, null, null), CancellationToken.None);
        var result = await service.CompleteAsync(opened.Value!.Id, new CompleteServiceTicketRequest(123.45m, "naprawiony", AssetStatus.InStock), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AssetStatus.InStock, asset.Status);
        Assert.Equal(ServiceTicketStatus.Completed, result.Value!.Status);
    }

    [Fact]
    public async Task CompleteAsync_InvalidResultStatus_ReturnsValidationError()
    {
        var (service, user, tickets, assets, _) = CreateService();
        var asset = AddInStockAsset(user, assets);
        assets.Add(asset);

        var opened = await service.OpenAsync(new OpenServiceTicketRequest(asset.Id, null, "Vendor X", null, null, null, null), CancellationToken.None);
        var result = await service.CompleteAsync(opened.Value!.Id, new CompleteServiceTicketRequest(null, null, AssetStatus.Assigned), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AssetStatus.InService, asset.Status);
    }

    [Fact]
    public async Task CancelAsync_DoesNotChangeAssetStatus()
    {
        var (service, user, tickets, assets, _) = CreateService();
        var asset = AddInStockAsset(user, assets);
        assets.Add(asset);

        var opened = await service.OpenAsync(new OpenServiceTicketRequest(asset.Id, null, "Vendor X", null, null, null, null), CancellationToken.None);
        var result = await service.CancelAsync(opened.Value!.Id, new CancelServiceTicketRequest("anulowano"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ServiceTicketStatus.Cancelled, result.Value!.Status);
        Assert.Equal(AssetStatus.InService, asset.Status);
    }

    [Fact]
    public async Task CompleteAsync_AlreadyClosedTicket_ReturnsValidationError()
    {
        var (service, user, tickets, assets, _) = CreateService();
        var asset = AddInStockAsset(user, assets);
        assets.Add(asset);

        var opened = await service.OpenAsync(new OpenServiceTicketRequest(asset.Id, null, "Vendor X", null, null, null, null), CancellationToken.None);
        await service.CompleteAsync(opened.Value!.Id, new CompleteServiceTicketRequest(null, null, AssetStatus.InStock), CancellationToken.None);
        var result = await service.CompleteAsync(opened.Value!.Id, new CompleteServiceTicketRequest(null, null, AssetStatus.InStock), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task OpenAsync_RejectsRoleWithoutAccess()
    {
        var (service, user, tickets, assets, _) = CreateService();
        user.Roles = ["employee"];
        var asset = AddInStockAsset(user, assets);
        assets.Add(asset);

        var result = await service.OpenAsync(new OpenServiceTicketRequest(asset.Id, null, "Vendor X", null, null, null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task OpenAsync_RejectsCrossOrganizationAssetInspectionId()
    {
        var (service, user, tickets, assets, inspections) = CreateService();
        var asset = AddInStockAsset(user, assets);
        assets.Add(asset);
        var otherOrgInspection = new AssetInspection(Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow, "someone");
        inspections.Inspections.Add(otherOrgInspection);

        var result = await service.OpenAsync(new OpenServiceTicketRequest(asset.Id, otherOrgInspection.Id, "Vendor X", null, null, null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(tickets.Tickets);
    }
}
