using ClarioShop.Inventory.Persistence;
using ClarioShop.Inventory.Services;

namespace ClarioShop.Inventory.Features.Items;

/// <summary>Sets the stock level at which a variant shows as low, or clears it to use the store-wide default.</summary>
public static class SetReorderLevel
{
    /// <param name="ReorderLevel">Units, or kg for items sold by weight; null for the store-wide default.</param>
    public sealed record Request(decimal? ReorderLevel);

    public sealed record Command(Guid VariantId, decimal? ReorderLevel);

    public sealed record Response(Guid VariantId, string Sku, decimal? ReorderLevel);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.ReorderLevel).InclusiveBetween(0, 1_000_000).When(request => request.ReorderLevel is not null);
        }
    }

    internal sealed class Handler(InventoryDbContext db, ItemRegistry items) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> HandleAsync(Command command, CancellationToken cancellationToken) =>
            await ConcurrencyRetry.ExecuteAsync<Response>(db, async ct =>
            {
                // A product created a moment ago may not have reached Inventory through ProductCreated yet.
                if ((await items.EnsureKnownAsync([command.VariantId], ct)).IsFailure)
                {
                    return Error.NotFound("inventory.item_not_found", "The variant does not exist.");
                }

                var item = await db.Items.SingleAsync(item => item.VariantId == command.VariantId, ct);
                var result = item.SetReorderLevel(command.ReorderLevel);
                if (result.IsFailure)
                {
                    return result.Error;
                }

                await db.SaveChangesAsync(ct);
                return new Response(item.VariantId, item.Sku, item.ReorderLevel);
            }, cancellationToken);
    }
}
