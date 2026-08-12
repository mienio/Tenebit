using Tenebit.Domain.Common;
using Tenebit.Domain.Settings;

namespace Tenebit.Tests;

public class AssetStatusSettingTests
{
    [Theory]
    [InlineData("#1d4ed8")]
    [InlineData("#FFFFFF")]
    [InlineData("#000000")]
    public void Constructor_AcceptsValidHexColor(string color)
    {
        var setting = new AssetStatusSetting(Guid.NewGuid(), "Assigned", "Wydane", color, "#eff6ff", 10, true);
        Assert.Equal(color.ToLowerInvariant(), setting.Color);
    }

    [Theory]
    [InlineData("blue")]
    [InlineData("#123")]
    [InlineData("1d4ed8")]
    [InlineData("")]
    [InlineData("#gggggg")]
    public void Constructor_RejectsInvalidColor(string color)
    {
        Assert.Throws<DomainException>(() => new AssetStatusSetting(Guid.NewGuid(), "Assigned", "Wydane", color, "#eff6ff", 10, true));
    }

    [Theory]
    [InlineData("blue")]
    [InlineData("#123")]
    [InlineData("")]
    public void Constructor_RejectsInvalidBackgroundColor(string backgroundColor)
    {
        Assert.Throws<DomainException>(() => new AssetStatusSetting(Guid.NewGuid(), "Assigned", "Wydane", "#1d4ed8", backgroundColor, 10, true));
    }

    [Fact]
    public void Update_ChangesLabelColorsSortOrderAndEnabled()
    {
        var setting = new AssetStatusSetting(Guid.NewGuid(), "Assigned", "Wydane", "#1d4ed8", "#eff6ff", 10, true);

        setting.Update("Przypisane", "#047857", "#ecfdf5", 20, false);

        Assert.Equal("Przypisane", setting.Label);
        Assert.Equal("#047857", setting.Color);
        Assert.Equal("#ecfdf5", setting.BackgroundColor);
        Assert.Equal(20, setting.SortOrder);
        Assert.False(setting.IsEnabled);
    }
}
