using ClarioShop.Settings.Contracts;

namespace ClarioShop.Settings.Domain;

/// <summary>How amounts are shown. The currency itself is always BDT; only its presentation is configurable.</summary>
internal sealed record CurrencyFormat(string Symbol, CurrencySymbolPosition SymbolPosition, int DecimalDigits, DigitGrouping Grouping)
{
    public const string Code = "BDT";

    public static readonly CurrencyFormat Default = new("৳", CurrencySymbolPosition.Before, 2, DigitGrouping.SouthAsian);

    public CurrencyFormat Normalized() => this with { Symbol = Symbol.Trim() };
}
