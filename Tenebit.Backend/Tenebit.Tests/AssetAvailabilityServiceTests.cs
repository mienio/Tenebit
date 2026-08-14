using Tenebit.Application.Reservations;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Assignments;
using Tenebit.Domain.Reservations;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class AssetAvailabilityServiceTests
{
    private static (AssetAvailabilityService Service, InMemoryAssetRepository Assets, InMemoryAssignmentRepository Assignments, InMemoryEquipmentReservationRepository Reservations) CreateService()
    {
        var assets = new InMemoryAssetRepository();
        var assignments = new InMemoryAssignmentRepository();
        var reservations = new InMemoryEquipmentReservationRepository();
        var service = new AssetAvailabilityService(assets, assignments, reservations);
        return (service, assets, assignments, reservations);
    }

    private static Asset AddReservableAsset(InMemoryAssetRepository assets, Guid organizationId, Guid categoryId, AssetStatus status = AssetStatus.InStock, string? location = null)
    {
        var asset = new Asset(organizationId, categoryId, "Laptop", $"AT-{Guid.NewGuid():N}"[..8]);
        asset.SetReservationSettings(true, null, null);
        if (status != AssetStatus.InStock) asset.ChangeStatus(status);
        asset.UpdateCore("Laptop", asset.AssetTag, null, null, location, null, null, null, null, null, null, null);
        assets.Add(asset);
        return asset;
    }

    // Rezerwacja z pozycją wskazującą konkretne AssetId (nadawane dopiero przy zatwierdzeniu — poza zakresem
    // tego zadania) — dla testu ustawiamy je bezpośrednio przez refleksję, bo domena celowo nie wystawia jeszcze
    // takiej metody.
    private static void SetAssetId(EquipmentReservationItem item, Guid assetId) =>
        typeof(EquipmentReservationItem).GetProperty(nameof(EquipmentReservationItem.AssetId))!.SetValue(item, assetId);

    [Fact]
    public async Task CountAvailableAsync_CountsReservableInStockAssetsInCategory()
    {
        var (service, assets, _, _) = CreateService();
        var organizationId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        AddReservableAsset(assets, organizationId, categoryId);
        AddReservableAsset(assets, organizationId, categoryId);

        var count = await service.CountAvailableAsync(organizationId, categoryId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), CancellationToken.None);

        Assert.Equal(2, count);
    }

    [Theory]
    [InlineData(AssetStatus.Damaged)]
    [InlineData(AssetStatus.Lost)]
    [InlineData(AssetStatus.Retired)]
    [InlineData(AssetStatus.Disposed)]
    [InlineData(AssetStatus.InService)]
    [InlineData(AssetStatus.PendingReturn)]
    public async Task CountAvailableAsync_ExcludesAssetsWithUnavailableStatus(AssetStatus status)
    {
        var (service, assets, _, _) = CreateService();
        var organizationId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        AddReservableAsset(assets, organizationId, categoryId, status);

        var count = await service.CountAvailableAsync(organizationId, categoryId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), CancellationToken.None);

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task CountAvailableAsync_ExcludesAssetNotMarkedReservable()
    {
        var (service, assets, _, _) = CreateService();
        var organizationId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var asset = new Asset(organizationId, categoryId, "Projektor", "AT-0001");
        assets.Add(asset);

        var count = await service.CountAvailableAsync(organizationId, categoryId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), CancellationToken.None);

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task CountAvailableAsync_ExcludesAssetWithOverlappingApprovedReservation()
    {
        var (service, assets, _, reservations) = CreateService();
        var organizationId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var asset = AddReservableAsset(assets, organizationId, categoryId);

        var from = DateTimeOffset.UtcNow;
        var to = from.AddDays(3);
        var reservation = new EquipmentReservation(organizationId, Guid.NewGuid(), from.AddDays(1), from.AddDays(2), "Delegacja", null, null);
        var item = reservation.AddItem(categoryId, 1, null);
        SetAssetId(item, asset.Id);
        typeof(EquipmentReservation).GetProperty(nameof(EquipmentReservation.Status))!.SetValue(reservation, EquipmentReservationStatus.Approved);
        reservations.Add(reservation);

        var count = await service.CountAvailableAsync(organizationId, categoryId, from, to, CancellationToken.None);

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task CountAvailableAsync_DoesNotExcludeAssetWhenApprovedReservationDoesNotOverlap()
    {
        var (service, assets, _, reservations) = CreateService();
        var organizationId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var asset = AddReservableAsset(assets, organizationId, categoryId);

        var reservation = new EquipmentReservation(organizationId, Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(10), DateTimeOffset.UtcNow.AddDays(11), "Delegacja", null, null);
        var item = reservation.AddItem(categoryId, 1, null);
        SetAssetId(item, asset.Id);
        typeof(EquipmentReservation).GetProperty(nameof(EquipmentReservation.Status))!.SetValue(reservation, EquipmentReservationStatus.Approved);
        reservations.Add(reservation);

        var count = await service.CountAvailableAsync(organizationId, categoryId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), CancellationToken.None);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task CountAvailableAsync_ExcludesAssetWithOpenAssignmentOverlappingTerm()
    {
        var (service, assets, assignments, _) = CreateService();
        var organizationId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var asset = AddReservableAsset(assets, organizationId, categoryId);

        var from = DateTimeOffset.UtcNow;
        var to = from.AddDays(3);
        var assignment = new Assignment(organizationId, Guid.NewGuid(), "PR-0001", from.AddDays(-5), DateOnly.FromDateTime((from.AddDays(1)).UtcDateTime), null, "tester");
        assignment.AddAsset(asset.Id, null);
        assignments.Add(assignment);

        var count = await service.CountAvailableAsync(organizationId, categoryId, from, to, CancellationToken.None);

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task CountAvailableAsync_DoesNotExcludeAssetWhenOpenAssignmentDueDateBeforeTerm()
    {
        var (service, assets, assignments, _) = CreateService();
        var organizationId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var asset = AddReservableAsset(assets, organizationId, categoryId);

        var from = DateTimeOffset.UtcNow.AddDays(10);
        var to = from.AddDays(3);
        var assignment = new Assignment(organizationId, Guid.NewGuid(), "PR-0002", DateTimeOffset.UtcNow.AddDays(-10), DateOnly.FromDateTime(DateTimeOffset.UtcNow.AddDays(-1).UtcDateTime), null, "tester");
        assignment.AddAsset(asset.Id, null);
        assignments.Add(assignment);

        var count = await service.CountAvailableAsync(organizationId, categoryId, from, to, CancellationToken.None);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task CountAvailableAsync_FiltersByLocation()
    {
        var (service, assets, _, _) = CreateService();
        var organizationId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        AddReservableAsset(assets, organizationId, categoryId, location: "Warszawa");
        AddReservableAsset(assets, organizationId, categoryId, location: "Kraków");

        var count = await service.CountAvailableAsync(organizationId, categoryId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), "Warszawa", CancellationToken.None);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task CountAvailableAsync_IsScopedByOrganization()
    {
        var (service, assets, _, _) = CreateService();
        var organizationId = Guid.NewGuid();
        var otherOrganizationId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        AddReservableAsset(assets, otherOrganizationId, categoryId);

        var count = await service.CountAvailableAsync(organizationId, categoryId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), CancellationToken.None);

        Assert.Equal(0, count);
    }
}
