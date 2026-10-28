using ClarioShop.Catalog.Contracts;
using ClarioShop.Ordering.Domain;
using ClarioShop.Ordering.Persistence;

namespace ClarioShop.Ordering.Features.Carts;

public static class GetCart
{
    public sealed record Query(Guid CartId);

    internal sealed class Handler(OrderingDbContext db, ICatalogQueries catalog) : ICommandHandler<Query, CartDetails>
    {
        public async Task<Result<CartDetails>> HandleAsync(Query query, CancellationToken cancellationToken)
        {
            var cart = await db.Carts.AsNoTracking()
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.Id == query.CartId, cancellationToken);
            return cart is null ? OrderingErrors.CartNotFound : await CartDetails.PriceAsync(cart, catalog, cancellationToken);
        }
    }
}
