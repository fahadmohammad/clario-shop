using ClarioShop.Notifications.Domain;

namespace ClarioShop.Notifications.Tests.Domain;

public sealed class StaffAlertTemplateTests
{
    /// <summary>Enum names, since the enums are internal to the module.</summary>
    public static TheoryData<string, string, string> EveryTemplate()
    {
        var data = new TheoryData<string, string, string>();
        foreach (var kind in StaffAlertTemplates.Kinds)
        {
            foreach (var channel in Enum.GetNames<NotificationChannel>())
            {
                data.Add(kind.ToString(), channel, DefaultTemplates.English);
                data.Add(kind.ToString(), channel, DefaultTemplates.Bangla);
            }
        }

        return data;
    }

    [Fact]
    public void Kinds_AreTheStockAlerts()
    {
        var kinds = StaffAlertTemplates.Kinds;

        Assert.Equal([NotificationKind.StockWentNegative, NotificationKind.StockDriftDetected], kinds);
    }

    [Theory]
    [MemberData(nameof(EveryTemplate))]
    public void For_EveryKindChannelAndLanguage_FillsEveryPlaceholderAndNamesStoreAndSku(string kind, string channel, string language)
    {
        var parsed = Enum.Parse<NotificationKind>(kind);
        var template = StaffAlertTemplates.For(parsed, Enum.Parse<NotificationChannel>(channel), language);

        var message = template.Render(MessagePlaceholders.For(parsed).ToDictionary(name => name, name => name == MessagePlaceholders.StoreName ? "Clario Mart" : $"<{name}>"));

        Assert.Contains("Clario Mart", message.Body, StringComparison.Ordinal);
        Assert.Contains("<Sku>", message.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("{", message.Body + message.Subject, StringComparison.Ordinal);
        Assert.Equal(channel == nameof(NotificationChannel.Email), message.Subject is not null);
    }

    [Fact]
    public void For_StockWentNegativeSms_SaysWhatAndWhere()
    {
        var template = StaffAlertTemplates.For(NotificationKind.StockWentNegative, NotificationChannel.Sms, DefaultTemplates.English);

        var message = template.Render(new Dictionary<string, string>
        {
            [MessagePlaceholders.StoreName] = "Clario Mart",
            [MessagePlaceholders.ProductName] = "Miniket Rice 5kg",
            [MessagePlaceholders.Sku] = "RICE-5KG",
            [MessagePlaceholders.LocationCode] = "MAIN",
            [MessagePlaceholders.OnHand] = "-2",
        });

        Assert.Equal(
            "Clario Mart: Stock of Miniket Rice 5kg (RICE-5KG) at MAIN is below zero (-2). A sale was recorded without stock; please count it.",
            message.Body);
    }
}
