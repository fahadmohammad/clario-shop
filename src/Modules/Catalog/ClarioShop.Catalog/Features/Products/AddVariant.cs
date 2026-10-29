using ClarioShop.Catalog.Contracts.Events;
using ClarioShop.Catalog.Domain;
using ClarioShop.Catalog.Persistence;

namespace ClarioShop.Catalog.Features.Products;

/// <summary>Adds a variant (a new size, colour or pack) to an existing product; Inventory starts tracking it.</summary>
public static class AddVariant
{
    public sealed record Command(Guid ProductId, VariantInput Variant);

    internal sealed class Handler(CatalogDbContext db, TimeProvider clock) : ICommandHandler<Command, VariantDetails>
    {
        public async Task<Result<VariantDetails>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            var product = await db.LoadProductAsync(command.ProductId, cancellationToken);
            if (product is null)
            {
                return CatalogErrors.ProductNotFound;
            }

            var sku = Variant.NormalizeSku(command.Variant.Sku);
            if (await db.Variants.AnyAsync(v => v.Sku == sku && v.ProductId != product.Id, cancellationToken))
            {
                return CatalogErrors.SkuTaken(sku);
            }

            var added = product.AddVariant(command.Variant.ToSpec());
            if (added.IsFailure)
            {
                return added.Error;
            }

            var variant = added.Value;
            db.Publish(new VariantAdded(
                Guid.CreateVersion7(), clock.GetUtcNow(), product.Id, product.Name, variant.Id, variant.Sku, variant.Name, variant.Price));
            var saved = await db.SaveProductAsync(product, cancellationToken);
            return saved.IsFailure ? saved.Error : VariantDetails.From(variant);
        }
    }
}
