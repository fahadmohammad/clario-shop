using ClarioShop.Catalog.Domain;
using ClarioShop.Catalog.Persistence;

namespace ClarioShop.Catalog.Features.Brands;

/// <summary>Renames a brand. The slug stays, so storefront filter links keep working.</summary>
public static class RenameBrand
{
    public sealed record Request(string Name);

    public sealed record Command(Guid BrandId, Request Brand);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator() => RuleFor(request => request.Name).NotEmpty().MaximumLength(100);
    }

    internal sealed class Handler(CatalogDbContext db) : ICommandHandler<Command, BrandItem>
    {
        public async Task<Result<BrandItem>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            var brand = await db.Brands.SingleOrDefaultAsync(b => b.Id == command.BrandId, cancellationToken);
            if (brand is null)
            {
                return CatalogErrors.BrandNotFound;
            }

            var name = command.Brand.Name;
            if (await db.BrandNameTakenAsync(name, brand.Id, cancellationToken))
            {
                return CatalogErrors.BrandNameTaken(name.Trim());
            }

            brand.Rename(name);
            var saved = await db.SaveBrandAsync(brand, cancellationToken);
            return saved.IsFailure ? saved.Error : BrandItem.From(brand);
        }
    }
}
