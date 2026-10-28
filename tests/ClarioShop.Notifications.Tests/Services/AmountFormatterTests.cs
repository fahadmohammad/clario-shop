using ClarioShop.Notifications.Services;
using ClarioShop.Settings.Contracts;

namespace ClarioShop.Notifications.Tests.Services;

public sealed class AmountFormatterTests
{
    [Theory]
    [InlineData(0, "৳0.00")]
    [InlineData(250, "৳250.00")]
    [InlineData(1250.5, "৳1,250.50")]
    [InlineData(1234567.891, "৳12,34,567.89")]
    [InlineData(123456789, "৳12,34,56,789.00")]
    public void Format_SouthAsianGrouping_GroupsLakhAndCrore(decimal amount, string expected)
    {
        var currency = new CurrencyFormatInfo("BDT", "৳", CurrencySymbolPosition.Before, 2, DigitGrouping.SouthAsian);

        var formatted = AmountFormatter.Format(amount, currency);

        Assert.Equal(expected, formatted);
    }

    [Theory]
    [InlineData(1234567.5, "1,234,568 Tk")]
    [InlineData(999.49, "999 Tk")]
    [InlineData(1000, "1,000 Tk")]
    public void Format_ThousandsSymbolAfterNoDecimals_RoundsHalfAwayFromZero(decimal amount, string expected)
    {
        var currency = new CurrencyFormatInfo("BDT", "Tk", CurrencySymbolPosition.After, 0, DigitGrouping.Thousands);

        var formatted = AmountFormatter.Format(amount, currency);

        Assert.Equal(expected, formatted);
    }

    [Fact]
    public void Format_Negative_KeepsTheSign()
    {
        var currency = new CurrencyFormatInfo("BDT", "৳", CurrencySymbolPosition.Before, 0, DigitGrouping.SouthAsian);

        var formatted = AmountFormatter.Format(-1500m, currency);

        Assert.Equal("৳-1,500", formatted);
    }
}
