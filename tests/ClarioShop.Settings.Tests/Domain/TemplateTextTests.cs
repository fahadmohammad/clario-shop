using ClarioShop.Settings.Domain;

namespace ClarioShop.Settings.Tests.Domain;

public sealed class TemplateTextTests
{
    private static readonly string[] Placeholders = ["StoreName", "OrderNumber", "Total"];

    [Theory]
    [InlineData("")]
    [InlineData("Thank you for shopping with us!")]
    [InlineData("{StoreName}: order {OrderNumber}, total {Total}.")]
    [InlineData("{OrderNumber}{OrderNumber}")]
    [InlineData("{StoreName}: আপনার অর্ডার {OrderNumber} পাঠানো হয়েছে।")]
    public void FindProblem_OnlyKnownPlaceholders_IsNull(string text)
    {
        var problem = TemplateText.FindProblem(text, Placeholders);

        Assert.Null(problem);
    }

    [Theory]
    [InlineData("Order {CustomerName}", "{CustomerName} is not a placeholder")]
    [InlineData("Order {ordernumber}", "{ordernumber} is not a placeholder")]
    [InlineData("Order { OrderNumber }", "{ OrderNumber } is not a placeholder")]
    [InlineData("Order {}", "{} is not a placeholder")]
    [InlineData("Order {OrderNumber", "not closed or not opened")]
    [InlineData("Order OrderNumber}", "not closed or not opened")]
    [InlineData("Order {{OrderNumber}}", "not closed or not opened")]
    [InlineData("Order {Order{Number}", "not closed or not opened")]
    public void FindProblem_UnknownPlaceholderOrStrayBrace_SaysWhat(string text, string expected)
    {
        var problem = TemplateText.FindProblem(text, Placeholders);

        Assert.NotNull(problem);
        Assert.Contains(expected, problem, StringComparison.Ordinal);
    }

    [Fact]
    public void FindProblem_UnknownPlaceholder_ListsTheKnownOnes()
    {
        var problem = TemplateText.FindProblem("{Name}", Placeholders);

        Assert.EndsWith("Use {StoreName}, {OrderNumber}, {Total}.", problem, StringComparison.Ordinal);
    }
}
