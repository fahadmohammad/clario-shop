using ClarioShop.Ordering.Domain;

namespace ClarioShop.Ordering.Tests.Domain;

public sealed class OrderChargesTests
{
    [Fact]
    public void Calculate_NoDeliveryZone_ChargesNoDeliveryFee()
    {
        var charges = OrderCharges.Calculate(500m, delivery: null, vatRatePercent: 0m);

        Assert.Equal((0m, 500m), (charges.DeliveryFee, charges.Total));
    }

    [Theory]
    [InlineData(999.99, 60)]
    [InlineData(1000.00, 0)] // reaching the threshold is enough
    [InlineData(1500.00, 0)]
    public void Calculate_FreeDeliveryThreshold_WaivesFeeFromThreshold(decimal subtotal, decimal expectedFee)
    {
        var charges = OrderCharges.Calculate(subtotal, Zone(fee: 60m, threshold: 1000m), vatRatePercent: 0m);

        Assert.Equal(expectedFee, charges.DeliveryFee);
    }

    [Fact]
    public void Calculate_ZoneWithoutThreshold_AlwaysChargesFee()
    {
        var charges = OrderCharges.Calculate(100_000m, Zone(fee: 120m, threshold: null), vatRatePercent: 0m);

        Assert.Equal(120m, charges.DeliveryFee);
    }

    [Theory]
    [InlineData(1000.00, 15, 150.00)]
    [InlineData(1799.50, 7.5, 134.96)] // 134.9625
    [InlineData(33.30, 15, 5.00)] // 4.995, half away from zero
    [InlineData(33.26, 15, 4.99)] // 4.989
    [InlineData(0.03, 5, 0.00)] // 0.0015
    [InlineData(1250.00, 0, 0.00)]
    public void Calculate_Vat_IsRoundedToPoisha(decimal subtotal, decimal ratePercent, decimal expectedVat)
    {
        var charges = OrderCharges.Calculate(subtotal, delivery: null, ratePercent);

        Assert.Equal(expectedVat, charges.Vat);
    }

    [Fact]
    public void Calculate_Vat_IsOnGoodsOnlyNotOnDeliveryFee()
    {
        var charges = OrderCharges.Calculate(1000m, Zone(fee: 100m, threshold: null), vatRatePercent: 15m);

        Assert.Equal((1000m, 100m, 150m, 1250m), (charges.Subtotal, charges.DeliveryFee, charges.Vat, charges.Total));
    }

    private static OrderDelivery Zone(decimal fee, decimal? threshold) =>
        new(Guid.CreateVersion7(), "Inside Dhaka", fee, threshold);
}
