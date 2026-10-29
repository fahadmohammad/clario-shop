using ClarioShop.Catalog.Persistence;

namespace ClarioShop.Catalog.Features.Attributes;

public static class ListAttributes
{
    public sealed record Query;

    internal sealed class Handler(CatalogDbContext db) : ICommandHandler<Query, IReadOnlyList<AttributeItem>>
    {
        public async Task<Result<IReadOnlyList<AttributeItem>>> HandleAsync(Query query, CancellationToken cancellationToken)
        {
            var attributes = await db.Attributes.AsNoTracking()
                .Include(a => a.Values)
                .OrderBy(a => a.NormalizedName)
                .ToListAsync(cancellationToken);

            return attributes.Select(AttributeItem.From).ToList();
        }
    }
}
