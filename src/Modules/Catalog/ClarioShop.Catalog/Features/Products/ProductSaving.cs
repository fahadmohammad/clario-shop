using ClarioShop.Catalog.Domain;
using ClarioShop.Catalog.Persistence;
using Npgsql;

namespace ClarioShop.Catalog.Features.Products;

internal static class ProductSaving
{
    public static Task<Product?> LoadProductAsync(this CatalogDbContext db, Guid productId, CancellationToken cancellationToken) =>
        db.Products.Include(p => p.Variants).SingleOrDefaultAsync(p => p.Id == productId, cancellationToken);

    /// <summary>
    /// Saves a change to a product or its variants. The product row is always written, so its RowVersion turns two
    /// concurrent changes (say, two staff each taking a different last variant off sale, or a variant added while
    /// the product is archived) into a conflict instead of a product left in a state neither of them chose.
    /// </summary>
    public static async Task<Result> SaveProductAsync(this CatalogDbContext db, Product product, CancellationToken cancellationToken)
    {
        db.Entry(product).State = EntityState.Modified;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return CatalogErrors.Concurrency;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "PK_VariantAttributeValues",
        })
        {
            // Two changes gave the same variant a value for the same attribute at once.
            return CatalogErrors.Concurrency;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Only a new variant can break the unique SKU index; another product took the SKU since we checked.
            return CatalogErrors.SkuTaken(AddedSku(db));
        }
    }

    private static string AddedSku(CatalogDbContext db) =>
        db.ChangeTracker.Entries<Variant>()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity.Sku)
            .FirstOrDefault() ?? string.Empty;
}
