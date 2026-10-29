using ClarioShop.Inventory.Contracts;

namespace ClarioShop.Inventory.Features.Drift;

internal sealed class DriftEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory").WithTags("Inventory");

        group.MapPost("/drift-checks", static async (RunDriftCheck.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new RunDriftCheck.Command(), cancellationToken)).ToHttpResult())
            .WithName("RunDriftCheck")
            .WithSummary("Recompute every stock level from the ledger now, correcting and recording any that drifted.")
            .Produces<RunDriftCheck.Response>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(InventoryPermissions.Adjust);

        group.MapGet("/drifts", static async ([AsParameters] ListStockDrifts.Query query, ListStockDrifts.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(query, cancellationToken)).ToHttpResult())
            .WithName("ListStockDrifts")
            .WithSummary("Stock levels the drift check found out of step with the ledger, newest first.")
            .WithValidation<ListStockDrifts.Query>()
            .Produces<ListStockDrifts.Response>()
            .RequirePermission(InventoryPermissions.StockView);
    }
}
