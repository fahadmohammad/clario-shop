using ClarioShop.Catalog.Domain;
using ClarioShop.Catalog.Persistence;
using ClarioShop.Catalog.Services;

namespace ClarioShop.Catalog.Features.Brands;

public static class CreateBrand
{
    public sealed record Command(string Name);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator() => RuleFor(command => command.Name).NotEmpty().MaximumLength(100);
    }

    internal sealed class Handler(CatalogDbContext db, SlugAllocator slugs, TimeProvider clock) : ICommandHandler<Command, BrandItem>
    {
        public async Task<Result<BrandItem>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            if (await db.BrandNameTakenAsync(command.Name, null, cancellationToken))
            {
                return CatalogErrors.BrandNameTaken(command.Name.Trim());
            }

            var slug = await slugs.ForBrandAsync(command.Name, cancellationToken);
            var brand = Brand.Create(command.Name, slug, clock.GetUtcNow());

            db.Brands.Add(brand);
            var saved = await db.SaveBrandAsync(brand, cancellationToken);
            return saved.IsFailure ? saved.Error : BrandItem.From(brand);
        }
    }
}
