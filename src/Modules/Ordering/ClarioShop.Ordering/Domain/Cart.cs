using ClarioShop.SharedKernel.Domain;
using ClarioShop.SharedKernel.Results;

namespace ClarioShop.Ordering.Domain;

/// <summary>
/// A shopping cart, identified by an id the storefront keeps for guests. Holds variants and quantities only;
/// checkout turns it into exactly one order, after which it is closed.
/// </summary>
internal sealed class Cart : Entity
{
    public const int MaxItems = 50;

    public const decimal MaxQuantity = 1000;

    private readonly List<CartItem> items = [];

    private Cart()
    {
    }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>The order this cart was checked out into; set once, after which the cart cannot change.</summary>
    public Guid? OrderId { get; private set; }

    /// <summary>Concurrency token (PostgreSQL xmin). Every item change touches the cart row.</summary>
    public uint RowVersion { get; private set; }

    public IReadOnlyCollection<CartItem> Items => items;

    public static Cart Create(DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        CreatedAt = now,
        UpdatedAt = now,
    };

    /// <summary>Adds a variant, or adds to its quantity when it is already in the cart.</summary>
    public Result AddItem(Guid variantId, decimal quantity, DateTimeOffset now)
    {
        if (OrderId is not null)
        {
            return OrderingErrors.CartCheckedOut;
        }

        if (items.Find(item => item.VariantId == variantId) is { } existing)
        {
            return SetQuantity(existing, existing.Quantity + quantity, now);
        }

        if (items.Count >= MaxItems)
        {
            return OrderingErrors.TooManyCartItems;
        }

        if (quantity > MaxQuantity)
        {
            return OrderingErrors.QuantityTooLarge;
        }

        items.Add(CartItem.Create(Id, variantId, quantity, now));
        UpdatedAt = now;
        return Result.Success();
    }

    public Result UpdateItem(Guid variantId, decimal quantity, DateTimeOffset now)
    {
        if (OrderId is not null)
        {
            return OrderingErrors.CartCheckedOut;
        }

        return items.Find(item => item.VariantId == variantId) is { } item
            ? SetQuantity(item, quantity, now)
            : OrderingErrors.CartItemNotFound;
    }

    public Result RemoveItem(Guid variantId, DateTimeOffset now)
    {
        if (OrderId is not null)
        {
            return OrderingErrors.CartCheckedOut;
        }

        if (items.Find(item => item.VariantId == variantId) is not { } item)
        {
            return OrderingErrors.CartItemNotFound;
        }

        items.Remove(item);
        UpdatedAt = now;
        return Result.Success();
    }

    /// <summary>Closes the cart for <paramref name="orderId"/>.</summary>
    public Result CheckOut(Guid orderId, DateTimeOffset now)
    {
        if (OrderId is not null)
        {
            return OrderingErrors.CartCheckedOut;
        }

        if (items.Count == 0)
        {
            return OrderingErrors.CartEmpty;
        }

        OrderId = orderId;
        UpdatedAt = now;
        return Result.Success();
    }

    private Result SetQuantity(CartItem item, decimal quantity, DateTimeOffset now)
    {
        if (quantity > MaxQuantity)
        {
            return OrderingErrors.QuantityTooLarge;
        }

        item.SetQuantity(quantity);
        UpdatedAt = now;
        return Result.Success();
    }
}
