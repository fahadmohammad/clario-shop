using ClarioShop.Catalog.Contracts;
using ClarioShop.Ordering.Domain;
using ClarioShop.Ordering.Persistence;

namespace ClarioShop.Ordering.Features.Carts;

public static class RemoveCartItem
{
    public sealed record Command(Guid CartId, Guid VariantId);

    internal sealed class Handler(OrderingDbContext db, ICatalogQueries catalog, TimeProvider clock) : ICommandHandler<Command, CartDetails>
    {
        public async Task<Result<CartDetails>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            var cart = await db.FindCartAsync(command.CartId, cancellationToken);
            if (cart is null)
            {
                return OrderingErrors.CartNotFound;
            }

            var removed = cart.RemoveItem(command.VariantId, clock.GetUtcNow());
            return removed.IsFailure ? removed.Error : await db.SaveCartAsync(cart, catalog, cancellationToken);
        }
    }
}
