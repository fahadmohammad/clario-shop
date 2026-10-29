using ClarioShop.Catalog.Contracts.Events;
using ClarioShop.Inventory.Domain;
using ClarioShop.Inventory.Persistence;
using ClarioShop.Inventory.Services;
using ClarioShop.SharedKernel.Messaging;

namespace ClarioShop.Inventory.Features.Items;

/// <summary>Starts tracking a variant added to an existing product, with zero stock at the default location.</summary>
internal sealed class VariantAddedHandler(InventoryDbContext db, ItemRegistry items, TimeProvider clock)
    : IIntegrationEventHandler<VariantAdded>
{
    public async Task HandleAsync(VariantAdded integrationEvent, CancellationToken cancellationToken)
    {
        var result = await ConcurrencyRetry.ExecuteAsync<int>(db, async ct =>
        {
            var now = clock.GetUtcNow();
            var added = await items.AddMissingAsync(
                [InventoryItem.Create(integrationEvent.VariantId, integrationEvent.ProductId, integrationEvent.Sku, integrationEvent.ProductName, integrationEvent.Name, now)],
                ct);

            var defaultLocation = await db.Locations.Where(l => l.IsDefault).Select(l => l.Id).SingleAsync(ct);
            await db.LoadOrCreateAsync(defaultLocation, [integrationEvent.VariantId], now, ct);
            await db.SaveChangesAsync(ct);
            return added;
        }, cancellationToken);

        if (result.IsFailure)
        {
            // Let the outbox redeliver later rather than marking the event as handled.
            throw new InvalidOperationException($"Could not track variant {integrationEvent.VariantId}: {result.Error.Message}");
        }
    }
}
