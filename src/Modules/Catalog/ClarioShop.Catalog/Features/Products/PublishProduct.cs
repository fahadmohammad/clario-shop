using ClarioShop.Catalog.Contracts.Events;
using ClarioShop.Catalog.Domain;
using ClarioShop.Catalog.Persistence;

namespace ClarioShop.Catalog.Features.Products;

public static class PublishProduct
{
    public sealed record Command(Guid ProductId);

    public sealed record Response(Guid Id, string Slug, DateTimeOffset PublishedAt);

    internal sealed class Handler(CatalogDbContext db, TimeProvider clock) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            var product = await db.Products.Include(p => p.Variants).SingleOrDefaultAsync(p => p.Id == command.ProductId, cancellationToken);
            if (product is null)
            {
                return CatalogErrors.ProductNotFound;
            }

            var now = clock.GetUtcNow();
            var published = product.Publish(now);
            if (published.IsFailure)
            {
                return published.Error;
            }

            db.Publish(new ProductPublished(Guid.CreateVersion7(), now, product.Id, product.Slug));
            await db.SaveChangesAsync(cancellationToken);
            return new Response(product.Id, product.Slug, now);
        }
    }
}
