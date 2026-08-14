using Tenebit.Application.Reservations;
using Tenebit.Domain.Assets;
using Tenebit.Domain.People;
using Tenebit.Domain.Reservations;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

public class ReservationCatalogServiceTests
{
    private static (ReservationCatalogService Service, FakeCurrentUser User, InMemoryPersonRepository People, InMemoryAssetCategoryRepository Categories, InMemoryEquipmentKitDefinitionRepository Kits, InMemoryAssetRepository Assets) CreateService()
    {
        var currentUser = new FakeCurrentUser();
        var people = new InMemoryPersonRepository();
        var categories = new InMemoryAssetCategoryRepository();
        var kits = new InMemoryEquipmentKitDefinitionRepository();
        var assets = new InMemoryAssetRepository();
        var assignments = new InMemoryAssignmentRepository();
        var reservations = new InMemoryEquipmentReservationRepository();
        var availability = new AssetAvailabilityService(assets, assignments, reservations);
        var service = new ReservationCatalogService(people, categories, kits, availability, currentUser);
        return (service, currentUser, people, categories, kits, assets);
    }

    private static AssetCategory AddCatalogCategory(InMemoryAssetCategoryRepository categories, Guid organizationId, bool visible = true, string name = "Laptopy")
    {
        var category = new AssetCategory(organizationId, name, AssetCategoryType.Physical, null);
        category.UpdateCatalogSettings(visible, name, "Opis kategorii", null, ReservationMode.RequestByCategory);
        categories.Add(category);
        return category;
    }

    private static Asset AddReservableAsset(InMemoryAssetRepository assets, Guid organizationId, Guid categoryId)
    {
        var asset = new Asset(organizationId, categoryId, "Laptop", $"AT-{Guid.NewGuid():N}"[..8]);
        asset.SetReservationSettings(true, null, null);
        assets.Add(asset);
        return asset;
    }

    [Fact]
    public async Task GetAsync_ReturnsHasPersonRecordFalse_WhenUserNotLinkedToPerson()
    {
        var (service, _, _, _, _, _) = CreateService();

        var result = await service.GetAsync(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), null, null, CancellationToken.None);

        Assert.False(result.HasPersonRecord);
        Assert.Empty(result.Categories);
        Assert.Empty(result.Kits);
    }

    [Fact]
    public async Task GetAsync_ExcludesCategoriesNotVisibleInEmployeeCatalog()
    {
        var (service, user, people, categories, _, assets) = CreateService();
        people.Add(new Person(user.OrganizationId, "Jan", "Kowalski", user.Email));
        var visible = AddCatalogCategory(categories, user.OrganizationId, true, "Laptopy");
        AddCatalogCategory(categories, user.OrganizationId, false, "Ukryta kategoria");
        AddReservableAsset(assets, user.OrganizationId, visible.Id);

        var result = await service.GetAsync(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), null, null, CancellationToken.None);

        Assert.True(result.HasPersonRecord);
        var category = Assert.Single(result.Categories);
        Assert.Equal(visible.Id, category.Id);
        Assert.Equal(1, category.AvailableCount);
    }

    [Fact]
    public async Task GetAsync_FiltersCategoriesBySearchTerm()
    {
        var (service, user, people, categories, _, _) = CreateService();
        people.Add(new Person(user.OrganizationId, "Jan", "Kowalski", user.Email));
        AddCatalogCategory(categories, user.OrganizationId, true, "Laptopy");
        AddCatalogCategory(categories, user.OrganizationId, true, "Projektory");

        var result = await service.GetAsync(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), "projekt", null, CancellationToken.None);

        var category = Assert.Single(result.Categories);
        Assert.Equal("Projektory", category.Name);
    }

    [Fact]
    public async Task GetAsync_ComputesKitAvailabilityAsMinimumAcrossItems()
    {
        var (service, user, people, categories, kits, assets) = CreateService();
        people.Add(new Person(user.OrganizationId, "Jan", "Kowalski", user.Email));
        var laptopCategory = AddCatalogCategory(categories, user.OrganizationId, true, "Laptopy");
        var bagCategory = AddCatalogCategory(categories, user.OrganizationId, true, "Torby");
        AddReservableAsset(assets, user.OrganizationId, laptopCategory.Id);
        AddReservableAsset(assets, user.OrganizationId, laptopCategory.Id);
        AddReservableAsset(assets, user.OrganizationId, bagCategory.Id);

        var kit = new EquipmentKitDefinition(user.OrganizationId, "Laptop na podróż", null, true, "admin@acme.test", DateTimeOffset.UtcNow);
        kit.AddItem(laptopCategory.Id, 1);
        kit.AddItem(bagCategory.Id, 1);
        kits.Add(kit);

        var result = await service.GetAsync(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), null, null, CancellationToken.None);

        var kitResponse = Assert.Single(result.Kits);
        Assert.Equal(1, kitResponse.AvailableCount); // ograniczone przez 1 dostępną torbę
    }

    [Fact]
    public async Task GetAsync_ExcludesKitsNotVisibleInEmployeeCatalog()
    {
        var (service, user, people, _, kits, _) = CreateService();
        people.Add(new Person(user.OrganizationId, "Jan", "Kowalski", user.Email));
        var kit = new EquipmentKitDefinition(user.OrganizationId, "Ukryty zestaw", null, false, "admin@acme.test", DateTimeOffset.UtcNow);
        kits.Add(kit);

        var result = await service.GetAsync(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), null, null, CancellationToken.None);

        Assert.Empty(result.Kits);
    }

    [Fact]
    public async Task GetAsync_IsScopedByOrganization()
    {
        var (service, user, people, categories, _, assets) = CreateService();
        people.Add(new Person(user.OrganizationId, "Jan", "Kowalski", user.Email));
        var otherOrganizationId = Guid.NewGuid();
        AddCatalogCategory(categories, otherOrganizationId, true, "Z innej organizacji");

        var result = await service.GetAsync(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), null, null, CancellationToken.None);

        Assert.Empty(result.Categories);
    }
}
