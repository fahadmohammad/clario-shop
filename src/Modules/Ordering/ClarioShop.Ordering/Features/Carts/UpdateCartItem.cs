using ClarioShop.Catalog.Contracts;
using ClarioShop.Ordering.Domain;
using ClarioShop.Ordering.Persistence;

namespace ClarioShop.Ordering.Features.Carts;

public static class UpdateCartItem
{
    /// <param name="Quantity">The new quantity: units, or kg for items sold by weight.</param>
    public sealed record Request(decimal Quantity);

    public sealed record Command(Guid CartId, Guid VariantId, decimal Quantity);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator() => RuleFor(request => request.Quantity).ValidCartQuantity();
    }

    internal sealed class Handler(OrderingDbContext db, ICatalogQueries catalog, TimeProvider clock) : ICommandHandler<Command, CartDetails>
    {
        public async Task<Result<CartDetails>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            var cart = await db.FindCartAsync(command.CartId, cancellationToken);
            if (cart is null)
            {
                return OrderingErrors.CartNotFound;
            }

            if (cart.Items.All(item => item.VariantId != command.VariantId))
            {
                return OrderingErrors.CartItemNotFound;
            }

            var allowed = await CartItemRules.CheckAsync(catalog, command.VariantId, command.Quantity, cancellationToken);
            if (allowed.IsFailure)
            {
                return allowed.Error;
            }

            var updated = cart.UpdateItem(command.VariantId, command.Quantity, clock.GetUtcNow());
            return updated.IsFailure ? updated.Error : await db.SaveCartAsync(cart, catalog, cancellationToken);
        }
    }
}
