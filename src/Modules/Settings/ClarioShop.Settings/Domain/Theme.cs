namespace ClarioShop.Settings.Domain;

/// <summary>Storefront design tokens: colours as #RRGGBB (stored upper-case), radius in rem, font family names.</summary>
internal sealed record Theme(
    string PrimaryColor,
    string PrimaryForegroundColor,
    string AccentColor,
    string BackgroundColor,
    string ForegroundColor,
    decimal RadiusRem,
    string HeadingFont,
    string BodyFont,
    string? LogoUrl,
    string? FaviconUrl)
{
    public static readonly Theme Default = new(
        PrimaryColor: "#0F766E",
        PrimaryForegroundColor: "#FFFFFF",
        AccentColor: "#F59E0B",
        BackgroundColor: "#FFFFFF",
        ForegroundColor: "#0F172A",
        RadiusRem: 0.5m,
        HeadingFont: "Hind Siliguri",
        BodyFont: "Hind Siliguri",
        LogoUrl: null,
        FaviconUrl: null);

    public Theme Normalized() => this with
    {
        PrimaryColor = PrimaryColor.ToUpperInvariant(),
        PrimaryForegroundColor = PrimaryForegroundColor.ToUpperInvariant(),
        AccentColor = AccentColor.ToUpperInvariant(),
        BackgroundColor = BackgroundColor.ToUpperInvariant(),
        ForegroundColor = ForegroundColor.ToUpperInvariant(),
        HeadingFont = HeadingFont.Trim(),
        BodyFont = BodyFont.Trim(),
        LogoUrl = string.IsNullOrWhiteSpace(LogoUrl) ? null : LogoUrl.Trim(),
        FaviconUrl = string.IsNullOrWhiteSpace(FaviconUrl) ? null : FaviconUrl.Trim(),
    };
}
