using ClarioShop.Catalog.Domain;
using ClarioShop.Catalog.Persistence;

namespace ClarioShop.Catalog.Features.Products;

/// <summary>
/// Renames a variant, replaces its barcodes or takes it off sale. SKU and sold-by-weight stay as created, because
/// stock, orders and scale labels already refer to them; prices change through <see cref="ChangeVariantPrice"/>.
/// </summary>
public static class UpdateVariant
{
    public sealed record Request(string? Name, IReadOnlyList<string>? Barcodes, bool IsActive);

    public sealed record Command(Guid ProductId, Guid VariantId, Request Variant);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Name).MaximumLength(100);
            RuleForEach(request => request.Barcodes).NotEmpty().MaximumLength(64);
        }
    }

    internal sealed class Handler(CatalogDbContext db) : ICommandHandler<Command, VariantDetails>
    {
        public async Task<Result<VariantDetails>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            var product = await db.LoadProductAsync(command.ProductId, cancellationToken);
            if (product is null)
            {
                return CatalogErrors.ProductNotFound;
            }

            var request = command.Variant;
            var updated = product.UpdateVariant(command.VariantId, request.Name, request.Barcodes, request.IsActive);
            if (updated.IsFailure)
            {
                return updated.Error;
            }

            var saved = await db.SaveProductAsync(product, cancellationToken);
            return saved.IsFailure ? saved.Error : VariantDetails.From(updated.Value);
        }
    }
}
