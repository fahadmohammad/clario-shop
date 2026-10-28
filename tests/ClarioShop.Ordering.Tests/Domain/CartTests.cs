using ClarioShop.Ordering.Domain;
using ClarioShop.SharedKernel.Results;

namespace ClarioShop.Ordering.Tests.Domain;

public sealed class CartTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AddItem_SameVariantTwice_MergesQuantity()
    {
        var cart = Cart.Create(Now);
        var variantId = Guid.CreateVersion7();

        cart.AddItem(variantId, 2, Now);
        var added = cart.AddItem(variantId, 3, Now);

        Assert.True(added.IsSuccess);
        Assert.Equal(5, Assert.Single(cart.Items).Quantity);
    }

    [Fact]
    public void AddItem_MergedQuantityOverLimit_IsRefused()
    {
        var cart = Cart.Create(Now);
        var variantId = Guid.CreateVersion7();
        cart.AddItem(variantId, Cart.MaxQuantity, Now);

        var added = cart.AddItem(variantId, 1, Now);

        Assert.Equal(OrderingErrors.QuantityTooLarge, added.Error);
        Assert.Equal(Cart.MaxQuantity, Assert.Single(cart.Items).Quantity);
    }

    [Fact]
    public void AddItem_BeyondMaxDistinctItems_IsRefused()
    {
        var cart = Cart.Create(Now);
        for (var i = 0; i < Cart.MaxItems; i++)
        {
            cart.AddItem(Guid.CreateVersion7(), 1, Now);
        }

        var added = cart.AddItem(Guid.CreateVersion7(), 1, Now);

        Assert.Equal(OrderingErrors.TooManyCartItems, added.Error);
    }

    [Fact]
    public void UpdateItem_SetsQuantity()
    {
        var cart = Cart.Create(Now);
        var variantId = Guid.CreateVersion7();
        cart.AddItem(variantId, 2, Now);

        cart.UpdateItem(variantId, 1.25m, Now.AddMinutes(1));

        Assert.Equal(1.25m, Assert.Single(cart.Items).Quantity);
        Assert.Equal(Now.AddMinutes(1), cart.UpdatedAt);
    }

    [Fact]
    public void UpdateItem_NotInCart_ReturnsNotFound()
    {
        var cart = Cart.Create(Now);

        var updated = cart.UpdateItem(Guid.CreateVersion7(), 1, Now);

        Assert.Equal(OrderingErrors.CartItemNotFound, updated.Error);
    }

    [Fact]
    public void RemoveItem_TakesItOut()
    {
        var cart = Cart.Create(Now);
        var keep = Guid.CreateVersion7();
        var remove = Guid.CreateVersion7();
        cart.AddItem(keep, 1, Now);
        cart.AddItem(remove, 1, Now);

        cart.RemoveItem(remove, Now);

        Assert.Equal(keep, Assert.Single(cart.Items).VariantId);
    }

    [Fact]
    public void CheckOut_Empty_IsRefused()
    {
        var cart = Cart.Create(Now);

        var checkedOut = cart.CheckOut(Guid.CreateVersion7(), Now);

        Assert.Equal(OrderingErrors.CartEmpty, checkedOut.Error);
        Assert.Null(cart.OrderId);
    }

    [Fact]
    public void CheckedOutCart_RefusesEveryChange()
    {
        var cart = Cart.Create(Now);
        var variantId = Guid.CreateVersion7();
        cart.AddItem(variantId, 1, Now);
        var orderId = Guid.CreateVersion7();
        cart.CheckOut(orderId, Now);

        Result[] attempts =
        [
            cart.AddItem(Guid.CreateVersion7(), 1, Now),
            cart.UpdateItem(variantId, 2, Now),
            cart.RemoveItem(variantId, Now),
            cart.CheckOut(Guid.CreateVersion7(), Now),
        ];

        Assert.All(attempts, attempt => Assert.Equal(OrderingErrors.CartCheckedOut, attempt.Error));
        Assert.Equal(orderId, cart.OrderId);
        Assert.Equal(1, Assert.Single(cart.Items).Quantity);
    }
}
