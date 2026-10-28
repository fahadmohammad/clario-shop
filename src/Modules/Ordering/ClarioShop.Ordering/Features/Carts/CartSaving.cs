using ClarioShop.Catalog.Contracts;
using ClarioShop.Ordering.Domain;
using ClarioShop.Ordering.Persistence;
using Npgsql;

namespace ClarioShop.Ordering.Features.Carts;

internal static class CartSaving
{
    /// <summary>Saves a changed cart; a concurrent change to the same cart is a conflict, not a lost update.</summary>
    public static async Task<Result<CartDetails>> SaveCartAsync(this OrderingDbContext db, Cart cart, ICatalogQueries catalog, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.IsRace())
        {
            return OrderingErrors.CartChanged;
        }

        return await CartDetails.PriceAsync(cart, catalog, cancellationToken);
    }

    /// <summary>A concurrency token mismatch, or a unique index hit by a concurrent insert (e.g. the same variant added twice at once).</summary>
    public static bool IsRace(this DbUpdateException exception) =>
        exception is DbUpdateConcurrencyException
        || exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    public static Task<Cart?> FindCartAsync(this OrderingDbContext db, Guid cartId, CancellationToken cancellationToken) =>
        db.Carts.Include(cart => cart.Items).FirstOrDefaultAsync(cart => cart.Id == cartId, cancellationToken);
}
