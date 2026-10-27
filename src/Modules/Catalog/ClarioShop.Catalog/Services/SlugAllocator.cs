using ClarioShop.Catalog.Domain;
using ClarioShop.Catalog.Persistence;

namespace ClarioShop.Catalog.Services;

/// <summary>Finds a free slug by adding -2, -3 … when the natural one is taken.</summary>
internal sealed class SlugAllocator(CatalogDbContext db)
{
    public Task<string> ForProductAsync(string name, CancellationToken cancellationToken) =>
        AllocateAsync(Slug.From(name, "product"), slug => db.Products.AnyAsync(p => p.Slug == slug, cancellationToken));

    public Task<string> ForCategoryAsync(string name, CancellationToken cancellationToken) =>
        AllocateAsync(Slug.From(name, "category"), slug => db.Categories.AnyAsync(c => c.Slug == slug, cancellationToken));

    private static async Task<string> AllocateAsync(string baseSlug, Func<string, Task<bool>> isTaken)
    {
        var candidate = baseSlug;
        for (var suffix = 2; await isTaken(candidate); suffix++)
        {
            candidate = $"{baseSlug}-{suffix}";
        }

        return candidate;
    }
}
