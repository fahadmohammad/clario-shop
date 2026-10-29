using ClarioShop.Catalog.Domain;
using ClarioShop.Catalog.Persistence;

namespace ClarioShop.Catalog.Features.Products;

/// <summary>Replaces a product's shared content. The slug stays, so storefront links keep working.</summary>
public static class UpdateProduct
{
    public sealed record Request(string Name, string? Description, Guid? CategoryId, string? Brand);

    public sealed record Command(Guid ProductId, Request Product);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Name).NotEmpty().MaximumLength(200);
            RuleFor(request => request.Description).MaximumLength(10_000);
            RuleFor(request => request.Brand).MaximumLength(100);
        }
    }

    internal sealed class Handler(CatalogDbContext db) : ICommandHandler<Command, ProductDetails>
    {
        public async Task<Result<ProductDetails>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            var product = await db.LoadProductAsync(command.ProductId, cancellationToken);
            if (product is null)
            {
                return CatalogErrors.ProductNotFound;
            }

            var request = command.Product;
            if (request.CategoryId is { } categoryId && !await db.Categories.AnyAsync(c => c.Id == categoryId, cancellationToken))
            {
                return Error.Validation(nameof(Request.CategoryId), "The category does not exist.");
            }

            var edited = product.Edit(request.Name, request.Description, request.CategoryId, request.Brand);
            if (edited.IsFailure)
            {
                return edited.Error;
            }

            var saved = await db.SaveProductAsync(product, cancellationToken);
            return saved.IsFailure ? saved.Error : ProductDetails.From(product);
        }
    }
}
