using ClarioShop.Catalog.Persistence;

namespace ClarioShop.Catalog.Features.Categories;

public static class ListCategories
{
    public sealed record Query;

    public sealed record CategoryItem(Guid Id, string Name, string Slug, Guid? ParentId, int SortOrder);

    internal sealed class Handler(CatalogDbContext db) : ICommandHandler<Query, IReadOnlyList<CategoryItem>>
    {
        public async Task<Result<IReadOnlyList<CategoryItem>>> HandleAsync(Query query, CancellationToken cancellationToken)
        {
            var categories = await db.Categories.AsNoTracking()
                .OrderBy(category => category.SortOrder)
                .ThenBy(category => category.Name)
                .Select(category => new CategoryItem(category.Id, category.Name, category.Slug, category.ParentId, category.SortOrder))
                .ToListAsync(cancellationToken);

            return categories;
        }
    }
}
