using System.Security.Claims;
using ClarioShop.Inventory.Contracts;
using ClarioShop.Inventory.Services;

namespace ClarioShop.Inventory.Features.Adjustments;

internal sealed class AdjustmentEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/adjustments").WithTags("Inventory");

        group.MapPost("/", static async (RecordStockAdjustment.Command command, ClaimsPrincipal user, RecordStockAdjustment.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(command with { UserId = user.UserId() }, cancellationToken))
                    .ToHttpResult(created => TypedResults.Created($"/api/inventory/adjustments/{created.Id}", created)))
            .WithName("RecordStockAdjustment")
            .WithSummary("Correct stock with a reason code (damage, expiry, theft, count correction, other).")
            .WithValidation<RecordStockAdjustment.Command>()
            .Produces<RecordStockAdjustment.Response>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(InventoryPermissions.Adjust);
    }
}
