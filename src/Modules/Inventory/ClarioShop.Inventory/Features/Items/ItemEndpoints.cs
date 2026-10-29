using ClarioShop.Inventory.Contracts;

namespace ClarioShop.Inventory.Features.Items;

internal sealed class ItemEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/items").WithTags("Inventory");

        group.MapPut("/{variantId:guid}/reorder-level", static async (Guid variantId, SetReorderLevel.Request request, SetReorderLevel.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new SetReorderLevel.Command(variantId, request.ReorderLevel), cancellationToken)).ToHttpResult())
            .WithName("SetReorderLevel")
            .WithSummary("Set the available stock at or below which a variant shows as low; null uses the store-wide default.")
            .WithValidation<SetReorderLevel.Request>()
            .Produces<SetReorderLevel.Response>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequirePermission(InventoryPermissions.Adjust);
    }
}
