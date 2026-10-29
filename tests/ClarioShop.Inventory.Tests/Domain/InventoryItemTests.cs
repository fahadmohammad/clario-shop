using ClarioShop.Inventory.Domain;
using ClarioShop.SharedKernel.Results;

namespace ClarioShop.Inventory.Tests.Domain;

public sealed class InventoryItemTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_HasNoReorderLevelOfItsOwn()
    {
        var item = NewItem();

        Assert.Null(item.ReorderLevel);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(12.5)]
    public void SetReorderLevel_ZeroOrMore_IsKept(decimal level)
    {
        var item = NewItem();

        var result = item.SetReorderLevel(level);

        Assert.True(result.IsSuccess);
        Assert.Equal(level, item.ReorderLevel);
    }

    [Fact]
    public void SetReorderLevel_Null_ClearsItBackToTheStoreDefault()
    {
        var item = NewItem();
        item.SetReorderLevel(10);

        var result = item.SetReorderLevel(null);

        Assert.True(result.IsSuccess);
        Assert.Null(item.ReorderLevel);
    }

    [Fact]
    public void SetReorderLevel_Negative_IsRefusedAndKeepsTheOldLevel()
    {
        var item = NewItem();
        item.SetReorderLevel(10);

        var result = item.SetReorderLevel(-1);

        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(10, item.ReorderLevel);
    }

    private static InventoryItem NewItem() =>
        InventoryItem.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "RICE-5KG", "Miniket Rice", "5 kg", Now);
}
