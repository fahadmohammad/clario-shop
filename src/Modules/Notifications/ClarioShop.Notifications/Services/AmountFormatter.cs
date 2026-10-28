using System.Globalization;
using System.Text;
using ClarioShop.Settings.Contracts;

namespace ClarioShop.Notifications.Services;

/// <summary>Writes an amount the way the store shows prices, e.g. <c>৳12,34,567</c> or <c>1,234,567.50 Tk</c>.</summary>
internal static class AmountFormatter
{
    public static string Format(decimal amount, CurrencyFormatInfo currency)
    {
        var rounded = Math.Round(Math.Abs(amount), currency.DecimalDigits, MidpointRounding.AwayFromZero);
        var digits = rounded.ToString($"F{currency.DecimalDigits}", CultureInfo.InvariantCulture);
        var point = digits.IndexOf('.', StringComparison.Ordinal);
        var whole = point < 0 ? digits : digits[..point];
        var fraction = point < 0 ? string.Empty : digits[point..];

        var number = $"{(amount < 0 && rounded != 0 ? "-" : string.Empty)}{Group(whole, currency.Grouping)}{fraction}";
        return currency.SymbolPosition == CurrencySymbolPosition.Before
            ? $"{currency.Symbol}{number}"
            : $"{number} {currency.Symbol}";
    }

    /// <summary>Thousands: groups of three. South Asian: the last three digits, then groups of two (lakh, crore).</summary>
    private static string Group(string whole, DigitGrouping grouping)
    {
        if (whole.Length <= 3)
        {
            return whole;
        }

        var result = new StringBuilder(whole[^3..]);
        var rest = whole[..^3];
        var size = grouping == DigitGrouping.SouthAsian ? 2 : 3;
        while (rest.Length > 0)
        {
            var take = Math.Min(size, rest.Length);
            result.Insert(0, ',').Insert(0, rest[^take..]);
            rest = rest[..^take];
        }

        return result.ToString();
    }
}
