using ClarioShop.Catalog.Contracts.Events;
using ClarioShop.Inventory.Domain;
using ClarioShop.Inventory.Persistence;
using ClarioShop.Inventory.Services;
using ClarioShop.SharedKernel.Messaging;

namespace ClarioShop.Inventory.Features.Items;

/// <summary>Starts tracking a new product's variants, with zero stock at the default location.</summary>
internal sealed class ProductCreatedHandler(InventoryDbContext db, ItemRegistry items, TimeProvider clock)
    : IIntegrationEventHandler<ProductCreated>
{
    public async Task HandleAsync(ProductCreated integrationEvent, CancellationToken cancellationToken)
    {
        var result = await ConcurrencyRetry.ExecuteAsync<int>(db, async ct =>
        {
            var now = clock.GetUtcNow();
            var added = await items.AddMissingAsync(
                integrationEvent.Variants
                    .Select(variant => InventoryItem.Create(variant.VariantId, integrationEvent.ProductId, variant.Sku, integrationEvent.Name, variant.Name, now))
                    .ToList(),
                ct);

            var defaultLocation = await db.Locations.Where(l => l.IsDefault).Select(l => l.Id).SingleAsync(ct);
            await db.LoadOrCreateAsync(defaultLocation, integrationEvent.Variants.Select(v => v.VariantId).ToList(), now, ct);
            await db.SaveChangesAsync(ct);
            return added;
        }, cancellationToken);

        if (result.IsFailure)
        {
            // Let the outbox redeliver later rather than marking the event as handled.
            throw new InvalidOperationException($"Could not track product {integrationEvent.ProductId}: {result.Error.Message}");
        }
    }
}
