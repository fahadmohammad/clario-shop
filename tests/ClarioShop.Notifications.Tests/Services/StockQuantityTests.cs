using ClarioShop.Notifications.Features.StockAlerts;

namespace ClarioShop.Notifications.Tests.Services;

public sealed class StockQuantityTests
{
    [Theory]
    [InlineData("-2.000", "-2")]
    [InlineData("1.500", "1.5")]
    [InlineData("0", "0")]
    [InlineData("-0.25", "-0.25")]
    public void Format_Quantity_DropsTrailingZeros(string quantity, string expected)
    {
        var text = StockQuantity.Format(decimal.Parse(quantity, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(expected, text);
    }
}
