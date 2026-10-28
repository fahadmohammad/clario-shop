using System.Text.Json.Serialization;

namespace ClarioShop.Settings.Contracts;

/// <param name="VatRatePercent">VAT added to taxable amounts, in percent (15 means 15%).</param>
/// <param name="InvoicePrefix">Upper-case prefix for order and invoice numbers, e.g. <c>CS</c>.</param>
/// <param name="DefaultLanguage">One of <paramref name="Languages"/>.</param>
/// <param name="Languages">ISO 639-1 codes the store offers: <c>bn</c>, <c>en</c>.</param>
public sealed record StoreSettingsInfo(
    StoreProfileInfo Profile,
    ThemeInfo Theme,
    string InvoicePrefix,
    decimal VatRatePercent,
    CurrencyFormatInfo Currency,
    string DefaultLanguage,
    IReadOnlyList<string> Languages);

public sealed record StoreProfileInfo(
    string StoreName,
    string? ContactPhone,
    string? ContactEmail,
    string? AddressLine1,
    string? AddressLine2,
    string? Area,
    string? City,
    string? PostalCode);

/// <summary>
/// Design tokens for the storefront and apps. Colours are <c>#RRGGBB</c>; the radius is in rem;
/// fonts are family names (e.g. <c>Hind Siliguri</c>).
/// </summary>
public sealed record ThemeInfo(
    string PrimaryColor,
    string PrimaryForegroundColor,
    string AccentColor,
    string BackgroundColor,
    string ForegroundColor,
    decimal RadiusRem,
    string HeadingFont,
    string BodyFont,
    string? LogoUrl,
    string? FaviconUrl);

/// <param name="Code">ISO 4217, always <c>BDT</c> today.</param>
/// <param name="Symbol">Shown with amounts, e.g. <c>৳</c> or <c>Tk</c>.</param>
/// <param name="DecimalDigits">Digits after the decimal point when showing amounts (0–2).</param>
public sealed record CurrencyFormatInfo(
    string Code,
    string Symbol,
    CurrencySymbolPosition SymbolPosition,
    int DecimalDigits,
    DigitGrouping Grouping);

[JsonConverter(typeof(JsonStringEnumConverter<CurrencySymbolPosition>))]
public enum CurrencySymbolPosition
{
    /// <summary><c>৳1,250</c></summary>
    Before,

    /// <summary><c>1,250 ৳</c></summary>
    After,
}

[JsonConverter(typeof(JsonStringEnumConverter<DigitGrouping>))]
public enum DigitGrouping
{
    /// <summary>Lakh and crore: <c>12,34,567</c>.</summary>
    SouthAsian,

    /// <summary>Thousands: <c>1,234,567</c>.</summary>
    Thousands,
}
