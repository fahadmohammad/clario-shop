using System.Security.Claims;
using ClarioShop.Infrastructure.Tenancy;
using ClarioShop.Inventory.Contracts;
using ClarioShop.Inventory.Services;
using ClarioShop.SharedKernel.Tenancy;

namespace ClarioShop.Inventory.Features.Transfers;

internal sealed class TransferEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/transfers").WithTags("Inventory").RequirePlanFeature(PlanFeatures.MultipleLocations);

        group.MapPost("/", static async (RecordStockTransfer.Command command, ClaimsPrincipal user, RecordStockTransfer.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(command with { UserId = user.UserId() }, cancellationToken))
                    .ToHttpResult(created => TypedResults.Created($"/api/inventory/transfers/{created.Id}", created)))
            .WithName("RecordStockTransfer")
            .WithSummary("Move available stock from one location to another (TransferOut and TransferIn movements, saved together).")
            .WithValidation<RecordStockTransfer.Command>()
            .Produces<RecordStockTransfer.Response>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(InventoryPermissions.Transfer);

        group.MapGet("/", static async ([AsParameters] ListStockTransfers.Query query, ListStockTransfers.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(query, cancellationToken)).ToHttpResult())
            .WithName("ListStockTransfers")
            .WithSummary("Stock transfers, newest first, optionally for one location (either side) or variant.")
            .WithValidation<ListStockTransfers.Query>()
            .Produces<ListStockTransfers.Response>()
            .RequirePermission(InventoryPermissions.StockView);

        group.MapGet("/{id:guid}", static async (Guid id, GetStockTransfer.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new GetStockTransfer.Query(id), cancellationToken)).ToHttpResult())
            .WithName("GetStockTransfer")
            .WithSummary("One stock transfer with its lines.")
            .Produces<GetStockTransfer.Response>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequirePermission(InventoryPermissions.StockView);
    }
}
