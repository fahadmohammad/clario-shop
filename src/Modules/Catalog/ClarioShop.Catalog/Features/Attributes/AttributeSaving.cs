using ClarioShop.Catalog.Domain;
using ClarioShop.Catalog.Persistence;
using Npgsql;

namespace ClarioShop.Catalog.Features.Attributes;

internal static class AttributeSaving
{
    public static Task<ProductAttribute?> LoadAttributeAsync(this CatalogDbContext db, Guid attributeId, CancellationToken cancellationToken) =>
        db.Attributes.Include(a => a.Values).SingleOrDefaultAsync(a => a.Id == attributeId, cancellationToken);

    /// <summary>
    /// Saves a new or changed attribute. Names and values were checked before, but another request may have taken
    /// one since; the unique indexes then turn that into the same conflict.
    /// </summary>
    public static async Task<Result> SaveAttributeAsync(
        this CatalogDbContext db, ProductAttribute attribute, string? value, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
        } violation)
        {
            return violation.ConstraintName == "IX_Attributes_NormalizedName" || value is null
                ? CatalogErrors.AttributeNameTaken(attribute.Name)
                : CatalogErrors.AttributeValueTaken(attribute.Name, value.Trim());
        }
    }

    public static Task<bool> AttributeNameTakenAsync(
        this CatalogDbContext db, string name, Guid? exceptAttributeId, CancellationToken cancellationToken)
    {
        var normalized = ProductAttribute.Normalize(name);
        return db.Attributes.AnyAsync(a => a.NormalizedName == normalized && a.Id != exceptAttributeId, cancellationToken);
    }
}
