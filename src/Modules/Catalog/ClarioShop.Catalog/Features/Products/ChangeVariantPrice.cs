using ClarioShop.Catalog.Contracts.Events;
using ClarioShop.Catalog.Domain;
using ClarioShop.Catalog.Persistence;
using ClarioShop.Infrastructure.Auditing;

namespace ClarioShop.Catalog.Features.Products;

/// <summary>
/// Sets a variant's price. Each change is audited (who, before, after, when) and published as
/// <see cref="ProductPriceChanged"/> in the same transaction; orders already placed keep the price they copied.
/// </summary>
public static class ChangeVariantPrice
{
    public sealed record Request(decimal Price, decimal? CompareAtPrice);

    public sealed record Command(Guid ProductId, Guid VariantId, Request Prices);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Price).GreaterThan(0).PrecisionScale(18, 2, ignoreTrailingZeros: true);
            RuleFor(request => request.CompareAtPrice).PrecisionScale(18, 2, ignoreTrailingZeros: true);
        }
    }

    internal sealed class Handler(CatalogDbContext db, AuditTrail audit, TimeProvider clock) : ICommandHandler<Command, VariantDetails>
    {
        public async Task<Result<VariantDetails>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            var product = await db.LoadProductAsync(command.ProductId, cancellationToken);
            if (product is null)
            {
                return CatalogErrors.ProductNotFound;
            }

            var changed = product.ChangePrice(command.VariantId, command.Prices.Price, command.Prices.CompareAtPrice);
            if (changed.IsFailure)
            {
                return changed.Error;
            }

            var change = changed.Value;
            var variant = change.Variant;
            if (!change.IsChange)
            {
                return VariantDetails.From(variant);
            }

            audit.Record(
                db,
                AuditActions.PriceChanged,
                nameof(Variant),
                variant.Id,
                new AuditedPrice(product.Id, variant.Sku, change.OldPrice, change.OldCompareAtPrice),
                new AuditedPrice(product.Id, variant.Sku, variant.Price, variant.CompareAtPrice));
            db.Publish(new ProductPriceChanged(
                Guid.CreateVersion7(),
                clock.GetUtcNow(),
                product.Id,
                variant.Id,
                variant.Sku,
                change.OldPrice,
                variant.Price,
                change.OldCompareAtPrice,
                variant.CompareAtPrice));

            var saved = await db.SaveProductAsync(product, cancellationToken);
            return saved.IsFailure ? saved.Error : VariantDetails.From(variant);
        }

        private sealed record AuditedPrice(Guid ProductId, string Sku, decimal Price, decimal? CompareAtPrice);
    }
}
