using Tenebit.Domain.Assets;

namespace Tenebit.Tests;

public class AssetCategoryCatalogSettingsTests
{
    private static AssetCategory CreateCategory() =>
        new(Guid.NewGuid(), "Laptopy", AssetCategoryType.Physical, "opis");

    [Fact]
    public void UpdateCatalogSettings_SetsFields()
    {
        var category = CreateCategory();

        category.UpdateCatalogSettings(true, "Laptop służbowy", "Dostępny do rezerwacji", "https://cdn.acme.test/laptop.png", ReservationMode.SelectExactAsset);

        Assert.True(category.VisibleInEmployeeCatalog);
        Assert.Equal("Laptop służbowy", category.CatalogName);
        Assert.Equal("Dostępny do rezerwacji", category.CatalogDescription);
        Assert.Equal("https://cdn.acme.test/laptop.png", category.CatalogImageUrl);
        Assert.Equal(ReservationMode.SelectExactAsset, category.ReservationMode);
    }

    [Fact]
    public void NewCategory_DefaultsToNotVisibleAndRequestByCategory()
    {
        var category = CreateCategory();

        Assert.False(category.VisibleInEmployeeCatalog);
        Assert.Null(category.CatalogName);
        Assert.Equal(ReservationMode.RequestByCategory, category.ReservationMode);
    }
}
