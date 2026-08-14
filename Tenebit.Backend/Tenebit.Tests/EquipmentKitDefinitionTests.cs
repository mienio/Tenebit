using Tenebit.Domain.Common;
using Tenebit.Domain.Reservations;

namespace Tenebit.Tests;

public class EquipmentKitDefinitionTests
{
    private static EquipmentKitDefinition CreateKit() =>
        new(Guid.NewGuid(), "Laptop na podróż", "Zestaw na delegację", true, "admin@acme.test", DateTimeOffset.UtcNow);

    [Fact]
    public void Constructor_InitializesFields()
    {
        var organizationId = Guid.NewGuid();
        var at = DateTimeOffset.UtcNow;

        var kit = new EquipmentKitDefinition(organizationId, "Laptop na podróż", "opis", true, "admin@acme.test", at);

        Assert.NotEqual(Guid.Empty, kit.Id);
        Assert.Equal(organizationId, kit.OrganizationId);
        Assert.Equal("Laptop na podróż", kit.Name);
        Assert.Equal("opis", kit.Description);
        Assert.True(kit.VisibleInEmployeeCatalog);
        Assert.Equal("admin@acme.test", kit.CreatedBy);
        Assert.Equal(at, kit.CreatedAt);
        Assert.Empty(kit.Items);
    }

    [Fact]
    public void AddItem_AddsItemWithCategoryAndQuantity()
    {
        var kit = CreateKit();
        var categoryId = Guid.NewGuid();

        var item = kit.AddItem(categoryId, 2);

        Assert.Single(kit.Items);
        Assert.Equal(kit.Id, item.KitDefinitionId);
        Assert.Equal(categoryId, item.AssetCategoryId);
        Assert.Equal(2, item.RequiredQuantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddItem_ThrowsWhenRequiredQuantityNotPositive(int quantity)
    {
        var kit = CreateKit();

        Assert.Throws<DomainException>(() => kit.AddItem(Guid.NewGuid(), quantity));
    }

    [Fact]
    public void Constructor_ThrowsWhenNameIsEmpty()
    {
        Assert.Throws<DomainException>(() =>
            new EquipmentKitDefinition(Guid.NewGuid(), " ", null, true, "admin@acme.test", DateTimeOffset.UtcNow));
    }
}
