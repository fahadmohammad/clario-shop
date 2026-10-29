using ClarioShop.Catalog.Contracts.Events;
using ClarioShop.Catalog.Domain;
using ClarioShop.Catalog.Persistence;
using ClarioShop.Catalog.Services;

namespace ClarioShop.Catalog.Features.Products;

public static class CreateProduct
{
    public sealed record Command(
        string Name,
        string? Description,
        Guid? CategoryId,
        Guid? BrandId,
        IReadOnlyList<VariantInput> Variants);

    public sealed record Response(Guid Id, string Slug, string Status);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
            RuleFor(command => command.Description).MaximumLength(10_000);
            RuleFor(command => command.Variants).NotEmpty();
            RuleForEach(command => command.Variants).SetValidator(new VariantInputValidator());
        }
    }

    internal sealed class Handler(CatalogDbContext db, SlugAllocator slugs, TimeProvider clock) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            if (command.CategoryId is { } categoryId && !await db.Categories.AnyAsync(c => c.Id == categoryId, cancellationToken))
            {
                return Error.Validation(nameof(Command.CategoryId), "The category does not exist.");
            }

            if (command.BrandId is { } brandId && !await db.Brands.AnyAsync(b => b.Id == brandId, cancellationToken))
            {
                return Error.Validation(nameof(Command.BrandId), "The brand does not exist.");
            }

            var skus = command.Variants.Select(v => v.Sku.Trim().ToUpperInvariant()).ToList();
            var takenSku = await db.Variants.Where(v => skus.Contains(v.Sku)).Select(v => v.Sku).FirstOrDefaultAsync(cancellationToken);
            if (takenSku is not null)
            {
                return CatalogErrors.SkuTaken(takenSku);
            }

            var attributeValues = await AttributeValueLookup.LoadAsync(
                db, command.Variants.SelectMany(v => v.AttributeValueIds ?? []), cancellationToken);
            if (attributeValues.IsFailure)
            {
                return attributeValues.Error;
            }

            var now = clock.GetUtcNow();
            var slug = await slugs.ForProductAsync(command.Name, cancellationToken);
            var created = Product.Create(
                command.Name,
                slug,
                command.Description,
                command.CategoryId,
                command.BrandId,
                command.Variants.Select(v => v.ToSpec(attributeValues.Value)).ToList(),
                now);
            if (created.IsFailure)
            {
                return created.Error;
            }

            var product = created.Value;
            db.Products.Add(product);
            db.Publish(new ProductCreated(
                Guid.CreateVersion7(),
                now,
                product.Id,
                product.Name,
                product.Slug,
                product.Variants.Select(v => new ProductCreated.VariantInfo(v.Id, v.Sku, v.Name, v.Price)).ToList()));

            await db.SaveChangesAsync(cancellationToken);
            return new Response(product.Id, product.Slug, product.Status.ToString());
        }
    }
}
