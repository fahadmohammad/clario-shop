using ClarioShop.Settings.Contracts;
using ClarioShop.Settings.Domain;

namespace ClarioShop.Settings.Services;

internal static class SettingsMapping
{
    public static StoreSettingsInfo ToInfo(this StoreSettings settings) => new(
        new StoreProfileInfo(
            settings.Profile.StoreName,
            settings.Profile.ContactPhone,
            settings.Profile.ContactEmail,
            settings.Profile.AddressLine1,
            settings.Profile.AddressLine2,
            settings.Profile.Area,
            settings.Profile.City,
            settings.Profile.PostalCode),
        settings.Theme.ToInfo(),
        settings.InvoicePrefix,
        settings.VatRatePercent,
        new CurrencyFormatInfo(
            CurrencyFormat.Code,
            settings.Currency.Symbol,
            settings.Currency.SymbolPosition,
            settings.Currency.DecimalDigits,
            settings.Currency.Grouping),
        settings.DefaultLanguage,
        [.. settings.Languages]);

    public static ThemeInfo ToInfo(this Theme theme) => new(
        theme.PrimaryColor,
        theme.PrimaryForegroundColor,
        theme.AccentColor,
        theme.BackgroundColor,
        theme.ForegroundColor,
        theme.RadiusRem,
        theme.HeadingFont,
        theme.BodyFont,
        theme.LogoUrl,
        theme.FaviconUrl);

    public static DeliveryZoneInfo ToInfo(this DeliveryZone zone) =>
        new(zone.Id, zone.Name, [.. zone.Areas], zone.Fee, zone.FreeDeliveryThreshold);

    public static NotificationTemplateInfo ToInfo(this NotificationTemplate template) =>
        new(template.Event, template.Channel, template.Language, template.Subject, template.Body);
}
