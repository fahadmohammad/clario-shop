using ClarioShop.Catalog.Contracts;
using ClarioShop.Ordering.Domain;
using ClarioShop.Ordering.Persistence;

namespace ClarioShop.Ordering.Features.Carts;

public static class CreateCart
{
    public sealed record Command;

    internal sealed class Handler(OrderingDbContext db, ICatalogQueries catalog, TimeProvider clock) : ICommandHandler<Command, CartDetails>
    {
        public async Task<Result<CartDetails>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            var cart = Cart.Create(clock.GetUtcNow());
            db.Carts.Add(cart);
            await db.SaveChangesAsync(cancellationToken);
            return await CartDetails.PriceAsync(cart, catalog, cancellationToken);
        }
    }
}
