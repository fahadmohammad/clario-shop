using ClarioShop.Catalog.Domain;
using ClarioShop.Catalog.Persistence;
using Npgsql;

namespace ClarioShop.Catalog.Features.Brands;

internal static class BrandSaving
{
    /// <summary>
    /// Saves a new or renamed brand. The name was checked before, but another request may have taken it since;
    /// the unique index then turns that into the same conflict.
    /// </summary>
    public static async Task<Result> SaveBrandAsync(this CatalogDbContext db, Brand brand, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_Brands_NormalizedName",
        })
        {
            return CatalogErrors.BrandNameTaken(brand.Name);
        }
    }

    public static Task<bool> BrandNameTakenAsync(this CatalogDbContext db, string name, Guid? exceptBrandId, CancellationToken cancellationToken)
    {
        var normalized = Brand.Normalize(name);
        return db.Brands.AnyAsync(b => b.NormalizedName == normalized && b.Id != exceptBrandId, cancellationToken);
    }
}
