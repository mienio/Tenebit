using Tenebit.Domain.Assets;
using Tenebit.Domain.Common;

namespace Tenebit.Tests;

public class AssetReservationSettingsTests
{
    private static Asset CreateAsset() => new(Guid.NewGuid(), Guid.NewGuid(), "Laptop", "LT-001");

    [Fact]
    public void SetReservationSettings_UpdatesFields()
    {
        var asset = CreateAsset();

        asset.SetReservationSettings(true, "Odbiór w magazynie głównym", 7);

        Assert.True(asset.IsReservable);
        Assert.Equal("Odbiór w magazynie głównym", asset.ReservationInstructions);
        Assert.Equal(7, asset.MaxReservationDays);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SetReservationSettings_ThrowsWhenMaxDaysNotPositive(int maxDays)
    {
        var asset = CreateAsset();

        Assert.Throws<DomainException>(() => asset.SetReservationSettings(true, null, maxDays));
    }

    [Fact]
    public void SetReservationSettings_AllowsNullMaxDays()
    {
        var asset = CreateAsset();

        asset.SetReservationSettings(true, "Instrukcja", null);

        Assert.True(asset.IsReservable);
        Assert.Null(asset.MaxReservationDays);
    }
}
