using ClarioShop.Settings.Contracts;
using ClarioShop.Settings.Domain;

namespace ClarioShop.Settings.Tests.Domain;

public sealed class StoreSettingsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateDefault_NamesStoreAndUsesBdtDefaults()
    {
        var settings = StoreSettings.CreateDefault("  Rahim Store ", Now);

        Assert.Equal(StoreSettings.SingletonId, settings.Id);
        Assert.Equal("Rahim Store", settings.Profile.StoreName);
        Assert.Equal(("INV", 0m, "en"), (settings.InvoicePrefix, settings.VatRatePercent, settings.DefaultLanguage));
        Assert.Equal(["en", "bn"], settings.Languages);
        Assert.Equal(CurrencyFormat.Default, settings.Currency);
        Assert.Equal(Theme.Default, settings.Theme);
    }

    [Fact]
    public void Update_NormalizesPrefixLanguagesAndBlankProfileValues()
    {
        var settings = StoreSettings.CreateDefault("Store", Now);
        var later = Now.AddHours(1);

        var result = settings.Update(
            new StoreProfile(" Clario Mart ", "  ", " Hello@Clario.test ", "House 1", "", " Dhanmondi ", "Dhaka", null),
            " cs-bd ",
            15m,
            new CurrencyFormat(" Tk ", CurrencySymbolPosition.After, 0, DigitGrouping.Thousands),
            " BN ",
            ["en", "EN", "bn"],
            later);

        Assert.True(result.IsSuccess);
        Assert.Equal(new StoreProfile("Clario Mart", null, "hello@clario.test", "House 1", null, "Dhanmondi", "Dhaka", null), settings.Profile);
        Assert.Equal(("CS-BD", 15m, "bn"), (settings.InvoicePrefix, settings.VatRatePercent, settings.DefaultLanguage));
        Assert.Equal(["bn", "en"], settings.Languages);
        Assert.Equal("Tk", settings.Currency.Symbol);
        Assert.Equal(later, settings.UpdatedAt);
    }

    [Fact]
    public void Update_DefaultLanguageNotOffered_FailsAndChangesNothing()
    {
        var settings = StoreSettings.CreateDefault("Store", Now);

        var result = settings.Update(
            StoreProfile.Named("Other"), "NEW", 15m, CurrencyFormat.Default, "bn", ["en"], Now.AddHours(1));

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Equal(("Store", "INV", Now), (settings.Profile.StoreName, settings.InvoicePrefix, settings.UpdatedAt));
    }

    [Fact]
    public void UpdateTheme_UpperCasesColoursAndDropsBlankUrls()
    {
        var settings = StoreSettings.CreateDefault("Store", Now);

        settings.UpdateTheme(
            Theme.Default with { PrimaryColor = "#ff5722", HeadingFont = " Noto Sans Bengali ", LogoUrl = " https://cdn.test/logo.png ", FaviconUrl = " " },
            Now);

        Assert.Equal("#FF5722", settings.Theme.PrimaryColor);
        Assert.Equal("Noto Sans Bengali", settings.Theme.HeadingFont);
        Assert.Equal("https://cdn.test/logo.png", settings.Theme.LogoUrl);
        Assert.Null(settings.Theme.FaviconUrl);
    }
}
