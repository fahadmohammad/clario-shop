using ClarioShop.Catalog.Domain;
using ClarioShop.Catalog.Persistence;
using ClarioShop.Catalog.Services;

namespace ClarioShop.Catalog.Features.Categories;

public static class CreateCategory
{
    public sealed record Command(string Name, Guid? ParentId, int SortOrder = 0);

    public sealed record Response(Guid Id, string Name, string Slug);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator() => RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
    }

    internal sealed class Handler(CatalogDbContext db, SlugAllocator slugs) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            if (command.ParentId is { } parentId && !await db.Categories.AnyAsync(c => c.Id == parentId, cancellationToken))
            {
                return Error.Validation(nameof(Command.ParentId), "The parent category does not exist.");
            }

            var slug = await slugs.ForCategoryAsync(command.Name, cancellationToken);
            var category = Category.Create(command.Name, slug, command.ParentId, command.SortOrder);

            db.Categories.Add(category);
            await db.SaveChangesAsync(cancellationToken);
            return new Response(category.Id, category.Name, category.Slug);
        }
    }
}
