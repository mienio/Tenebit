using Tenebit.Application.Assets;

namespace Tenebit.Tests;

public class StarterAssetCategoryTranslationsTests
{
    [Theory]
    [InlineData("en", "Laptops")]
    [InlineData("es", "Portátiles")]
    [InlineData("de", "Laptops")]
    public void TranslateName_ReturnsTranslatedName_ForSystemCategoryInSupportedLanguage(string language, string expected)
    {
        var result = StarterAssetCategoryTranslations.TranslateName(isSystem: true, language, "Laptopy");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TranslateName_ReturnsOriginalName_ForPolish()
    {
        var result = StarterAssetCategoryTranslations.TranslateName(isSystem: true, "pl", "Laptopy");
        Assert.Equal("Laptopy", result);
    }

    [Fact]
    public void TranslateName_ReturnsOriginalName_WhenCategoryIsNotSystem()
    {
        var result = StarterAssetCategoryTranslations.TranslateName(isSystem: false, "en", "Laptopy");
        Assert.Equal("Laptopy", result);
    }

    [Fact]
    public void TranslateName_ReturnsOriginalName_WhenNoTranslationExists()
    {
        var result = StarterAssetCategoryTranslations.TranslateName(isSystem: true, "en", "Niestandardowa kategoria");
        Assert.Equal("Niestandardowa kategoria", result);
    }

    [Theory]
    [InlineData("en", "Portable computers and workstations.")]
    [InlineData("es", "Ordenadores portátiles y estaciones de trabajo.")]
    [InlineData("de", "Tragbare Computer und Workstations.")]
    public void TranslateDescription_ReturnsTranslatedDescription_ForSystemCategoryInSupportedLanguage(string language, string expected)
    {
        var result = StarterAssetCategoryTranslations.TranslateDescription(isSystem: true, language, "Laptopy", "Przenośne komputery i stacje robocze.");
        Assert.Equal(expected, result);
    }
}
