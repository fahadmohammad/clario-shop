using ClarioShop.Inventory.Persistence;

namespace ClarioShop.Inventory.Features.Stock;

public static class ListMovements
{
    public sealed record Query(Guid? VariantId, Guid? LocationId, int Page = 1, int PageSize = 50);

    public sealed record MovementItem(
        Guid Id,
        Guid VariantId,
        Guid LocationId,
        decimal Quantity,
        string Type,
        string? Reason,
        string ReferenceType,
        Guid ReferenceId,
        DateTimeOffset OccurredAt,
        DateTimeOffset RecordedAt,
        Guid? UserId,
        decimal? UnitCost);

    public sealed record Response(IReadOnlyList<MovementItem> Items, int Page, int PageSize, int TotalCount);

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
            RuleFor(query => query.PageSize).InclusiveBetween(1, 200);
        }
    }

    internal sealed class Handler(InventoryDbContext db) : ICommandHandler<Query, Response>
    {
        public async Task<Result<Response>> HandleAsync(Query query, CancellationToken cancellationToken)
        {
            var movements = db.Movements.AsNoTracking();
            if (query.VariantId is { } variantId)
            {
                movements = movements.Where(m => m.VariantId == variantId);
            }

            if (query.LocationId is { } locationId)
            {
                movements = movements.Where(m => m.LocationId == locationId);
            }

            var total = await movements.CountAsync(cancellationToken);
            var page = await movements
                .OrderByDescending(m => m.RecordedAt)
                .ThenByDescending(m => m.Id)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(cancellationToken);

            var items = page.Select(m => new MovementItem(
                    m.Id,
                    m.VariantId,
                    m.LocationId,
                    m.Quantity,
                    m.Type.ToString(),
                    m.Reason?.ToString(),
                    m.ReferenceType,
                    m.ReferenceId,
                    m.OccurredAt,
                    m.RecordedAt,
                    m.UserId,
                    m.UnitCost))
                .ToList();

            return new Response(items, query.Page, query.PageSize, total);
        }
    }
}
