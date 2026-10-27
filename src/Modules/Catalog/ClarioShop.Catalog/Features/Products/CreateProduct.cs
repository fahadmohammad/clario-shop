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
        string? Brand,
        IReadOnlyList<VariantInput> Variants);

    public sealed record VariantInput(
        string Sku,
        string? Name,
        decimal Price,
        decimal? CompareAtPrice = null,
        IReadOnlyList<string>? Barcodes = null,
        bool SoldByWeight = false);

    public sealed record Response(Guid Id, string Slug, string Status);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
            RuleFor(command => command.Description).MaximumLength(10_000);
            RuleFor(command => command.Brand).MaximumLength(100);
            RuleFor(command => command.Variants).NotEmpty();
            RuleForEach(command => command.Variants).ChildRules(variant =>
            {
                variant.RuleFor(v => v.Sku).NotEmpty().MaximumLength(64).Matches("^[A-Za-z0-9._-]+$")
                    .WithMessage("SKU may contain letters, digits, dot, dash and underscore.");
                variant.RuleFor(v => v.Name).MaximumLength(100);
                variant.RuleFor(v => v.Price).GreaterThan(0).PrecisionScale(18, 2, ignoreTrailingZeros: true);
                variant.RuleForEach(v => v.Barcodes).NotEmpty().MaximumLength(64);
            });
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

            var skus = command.Variants.Select(v => v.Sku.Trim().ToUpperInvariant()).ToList();
            var takenSku = await db.Variants.Where(v => skus.Contains(v.Sku)).Select(v => v.Sku).FirstOrDefaultAsync(cancellationToken);
            if (takenSku is not null)
            {
                return Error.Conflict("catalog.sku_taken", $"SKU {takenSku} is already used by another product.");
            }

            var now = clock.GetUtcNow();
            var slug = await slugs.ForProductAsync(command.Name, cancellationToken);
            var created = Product.Create(
                command.Name,
                slug,
                command.Description,
                command.CategoryId,
                command.Brand,
                command.Variants.Select(v => new VariantSpec(v.Sku, v.Name, v.Price, v.CompareAtPrice, v.Barcodes, v.SoldByWeight)).ToList(),
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
