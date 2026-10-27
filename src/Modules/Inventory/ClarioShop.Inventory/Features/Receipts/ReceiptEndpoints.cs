using System.Security.Claims;
using ClarioShop.Inventory.Contracts;
using ClarioShop.Inventory.Services;

namespace ClarioShop.Inventory.Features.Receipts;

internal sealed class ReceiptEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/receipts").WithTags("Inventory");

        group.MapPost("/", static async (RecordPurchaseReceipt.Command command, ClaimsPrincipal user, RecordPurchaseReceipt.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(command with { UserId = user.UserId() }, cancellationToken))
                    .ToHttpResult(created => TypedResults.Created($"/api/inventory/receipts/{created.Id}", created)))
            .WithName("RecordPurchaseReceipt")
            .WithSummary("Record goods received from a supplier; adds stock through PurchaseReceipt movements.")
            .WithValidation<RecordPurchaseReceipt.Command>()
            .Produces<RecordPurchaseReceipt.Response>(StatusCodes.Status201Created)
            .RequirePermission(InventoryPermissions.Receive);
    }
}
