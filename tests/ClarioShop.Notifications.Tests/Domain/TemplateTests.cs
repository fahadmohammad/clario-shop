using ClarioShop.Notifications.Domain;

namespace ClarioShop.Notifications.Tests.Domain;

public sealed class TemplateTests
{
    /// <summary>Enum names, since the enums are internal to the module.</summary>
    public static TheoryData<string, string, string> EveryTemplate()
    {
        var data = new TheoryData<string, string, string>();
        foreach (var kind in Enum.GetNames<OrderNotificationKind>())
        {
            foreach (var channel in Enum.GetNames<NotificationChannel>())
            {
                data.Add(kind, channel, DefaultTemplates.English);
                data.Add(kind, channel, DefaultTemplates.Bangla);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryTemplate))]
    public void For_EveryKindChannelAndLanguage_NamesStoreAndOrder(string kind, string channel, string language)
    {
        var template = DefaultTemplates.For(Enum.Parse<OrderNotificationKind>(kind), Enum.Parse<NotificationChannel>(channel), language);

        var message = template.Render(new Dictionary<string, string>
        {
            [MessagePlaceholders.StoreName] = "Clario Mart",
            [MessagePlaceholders.CustomerName] = "Rahim",
            [MessagePlaceholders.OrderNumber] = "CS-000123",
            [MessagePlaceholders.Total] = "৳1,250",
        });

        Assert.Contains("Clario Mart", message.Body, StringComparison.Ordinal);
        Assert.Contains("CS-000123", message.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("{", message.Body, StringComparison.Ordinal);
        Assert.Equal(channel == nameof(NotificationChannel.Email), message.Subject is not null);
    }

    [Theory]
    [InlineData("bn", "bn")]
    [InlineData("BN", "bn")]
    [InlineData("en", "en")]
    [InlineData("fr", "en")]
    [InlineData(null, "en")]
    public void LanguageFor_AnyLanguage_IsBanglaOrElseEnglish(string? language, string expected)
    {
        var result = DefaultTemplates.LanguageFor(language);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Render_PlaceholderWithoutValue_IsLeftAsItIs()
    {
        var template = new MessageTemplate("Order {OrderNumber}", "{StoreName}: total {Total}");

        var message = template.Render(new Dictionary<string, string> { [MessagePlaceholders.StoreName] = "Clario Mart", [MessagePlaceholders.OrderNumber] = "CS-000001" });

        Assert.Equal(("Order CS-000001", "Clario Mart: total {Total}"), (message.Subject, message.Body));
    }
}
