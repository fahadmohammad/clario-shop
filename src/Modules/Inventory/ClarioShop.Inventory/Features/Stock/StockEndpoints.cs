using ClarioShop.Inventory.Contracts;

namespace ClarioShop.Inventory.Features.Stock;

internal sealed class StockEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory").WithTags("Inventory");

        group.MapGet("/stock", static async ([AsParameters] ListStock.Query query, ListStock.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(query, cancellationToken)).ToHttpResult())
            .WithName("ListStock")
            .WithSummary("On-hand, reserved and available stock per variant and location.")
            .WithValidation<ListStock.Query>()
            .Produces<ListStock.Response>()
            .RequirePermission(InventoryPermissions.StockView);

        group.MapGet("/movements", static async ([AsParameters] ListMovements.Query query, ListMovements.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(query, cancellationToken)).ToHttpResult())
            .WithName("ListStockMovements")
            .WithSummary("The stock ledger, newest first.")
            .WithValidation<ListMovements.Query>()
            .Produces<ListMovements.Response>()
            .RequirePermission(InventoryPermissions.StockView);
    }
}
