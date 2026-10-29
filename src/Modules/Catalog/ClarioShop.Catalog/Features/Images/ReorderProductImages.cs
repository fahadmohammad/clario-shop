using ClarioShop.Catalog.Domain;
using ClarioShop.Catalog.Features.Products;
using ClarioShop.Catalog.Persistence;

namespace ClarioShop.Catalog.Features.Images;

/// <summary>Puts a product's gallery in a new order; the first image becomes the main one.</summary>
public static class ReorderProductImages
{
    public sealed record Request(IReadOnlyList<Guid> ImageIds);

    public sealed record Command(Guid ProductId, Request Order);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.ImageIds).NotNull()
                .Must(ids => ids is null || ids.Count <= Product.MaxImages)
                .WithMessage($"A product has at most {Product.MaxImages} images.");
        }
    }

    internal sealed class Handler(CatalogDbContext db) : ICommandHandler<Command, IReadOnlyList<ImageDetails>>
    {
        public async Task<Result<IReadOnlyList<ImageDetails>>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            var product = await db.LoadProductAsync(command.ProductId, cancellationToken);
            if (product is null)
            {
                return CatalogErrors.ProductNotFound;
            }

            var reordered = product.ReorderImages(command.Order.ImageIds);
            if (reordered.IsFailure)
            {
                return reordered.Error;
            }

            var saved = await db.SaveProductAsync(product, cancellationToken);
            return saved.IsFailure ? saved.Error : Result.Success(ImageDetails.ListOf(product));
        }
    }
}
