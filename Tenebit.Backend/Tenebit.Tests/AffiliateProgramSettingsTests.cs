using Tenebit.Domain.Affiliates;
using Tenebit.Domain.Common;

namespace Tenebit.Tests;

public class AffiliateProgramSettingsTests
{
    [Fact]
    public void CreateDefault_GrantsA20PercentCustomerDiscountFor3Months()
    {
        var settings = AffiliateProgramSettings.CreateDefault();

        Assert.True(settings.CodeGrantsCustomerDiscountByDefault);
        Assert.Equal(20m, settings.DefaultCustomerDiscountPercent);
        Assert.Equal(3, settings.DefaultCustomerDiscountDurationMonths);
    }

    [Fact]
    public void Update_AcceptsAValidCustomerDiscount()
    {
        var settings = AffiliateProgramSettings.CreateDefault();

        settings.Update(10m, AffiliateCommissionBase.Net, null, 10, 20, 5, null, true, 15m, 6, false);

        Assert.Equal(15m, settings.DefaultCustomerDiscountPercent);
        Assert.Equal(6, settings.DefaultCustomerDiscountDurationMonths);
    }

    [Fact]
    public void Update_RequiresAPercentWhenTheDiscountToggleIsOn()
    {
        var settings = AffiliateProgramSettings.CreateDefault();

        Assert.Throws<DomainException>(() => settings.Update(10m, AffiliateCommissionBase.Net, null, 10, 20, 5, null, true, null, 3, false));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Update_RejectsAnOutOfRangeCustomerDiscountPercent(decimal percent)
    {
        var settings = AffiliateProgramSettings.CreateDefault();

        Assert.Throws<DomainException>(() => settings.Update(10m, AffiliateCommissionBase.Net, null, 10, 20, 5, null, true, percent, 3, false));
    }

    [Fact]
    public void Update_RejectsAZeroOrNegativeDiscountDuration()
    {
        var settings = AffiliateProgramSettings.CreateDefault();

        Assert.Throws<DomainException>(() => settings.Update(10m, AffiliateCommissionBase.Net, null, 10, 20, 5, null, true, 20m, 0, false));
    }

    [Fact]
    public void Update_ClearsThePercentAndDurationWhenTheToggleIsTurnedOff()
    {
        var settings = AffiliateProgramSettings.CreateDefault();

        settings.Update(10m, AffiliateCommissionBase.Net, null, 10, 20, 5, null, false, 20m, 3, false);

        Assert.False(settings.CodeGrantsCustomerDiscountByDefault);
        Assert.Null(settings.DefaultCustomerDiscountPercent);
        Assert.Null(settings.DefaultCustomerDiscountDurationMonths);
    }

    [Fact]
    public void Update_AllowsNoDurationForALifetimeCustomerDiscount()
    {
        var settings = AffiliateProgramSettings.CreateDefault();

        settings.Update(10m, AffiliateCommissionBase.Net, null, 10, 20, 5, null, true, 20m, null, false);

        Assert.Equal(20m, settings.DefaultCustomerDiscountPercent);
        Assert.Null(settings.DefaultCustomerDiscountDurationMonths);
    }
}
