using System.Text.RegularExpressions;
using ClarioShop.Notifications.Services;

namespace ClarioShop.Notifications.Tests.Services;

public sealed partial class NotificationTemplateDefaultsTests
{
    [Fact]
    public void All_EveryEventChannelAndLanguage_ListedOnce()
    {
        var all = new NotificationTemplateDefaults().All;

        Assert.Equal(16, all.Count);
        Assert.Equal(16, all.Select(definition => (definition.Event, definition.Channel, definition.Language)).Distinct().Count());
        Assert.All(all, definition => Assert.Equal(definition.Channel == "Email", definition.Subject is not null));
    }

    [Fact]
    public void All_BuiltInText_UsesOnlyItsOwnPlaceholders()
    {
        var all = new NotificationTemplateDefaults().All;

        // Settings checks a store's own text against the same list, so the built-in text must pass it too.
        Assert.All(all, definition =>
        {
            var used = Placeholder().Matches($"{definition.Subject} {definition.Body}").Select(match => match.Groups[1].Value);
            Assert.Subset(definition.Placeholders.ToHashSet(), used.ToHashSet());
        });
    }

    [Theory]
    [InlineData("OrderPlaced", true)]
    [InlineData("OrderConfirmed", false)]
    [InlineData("OrderShipped", false)]
    [InlineData("OrderDelivered", false)]
    public void All_Total_OnlyWhenOrderIsPlaced(string eventName, bool hasTotal)
    {
        var definitions = new NotificationTemplateDefaults().All.Where(definition => definition.Event == eventName);

        Assert.All(definitions, definition => Assert.Equal(hasTotal, definition.Placeholders.Contains("Total")));
    }

    [GeneratedRegex(@"\{([^{}]*)\}")]
    private static partial Regex Placeholder();
}
