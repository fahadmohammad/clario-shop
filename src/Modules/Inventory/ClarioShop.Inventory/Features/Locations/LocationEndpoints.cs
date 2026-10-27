using ClarioShop.Inventory.Contracts;

namespace ClarioShop.Inventory.Features.Locations;

internal sealed class LocationEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/locations").WithTags("Inventory");

        group.MapGet("/", static async (ListLocations.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new ListLocations.Query(), cancellationToken)).ToHttpResult())
            .WithName("ListLocations")
            .WithSummary("Stock locations, default first.")
            .Produces<IReadOnlyList<ListLocations.LocationItem>>()
            .RequirePermission(InventoryPermissions.StockView);

        group.MapPost("/", static async (CreateLocation.Command command, CreateLocation.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(command, cancellationToken))
                    .ToHttpResult(created => TypedResults.Created($"/api/inventory/locations/{created.Id}", created)))
            .WithName("CreateLocation")
            .WithSummary("Add a branch or warehouse. The default location is created with the tenant.")
            .WithValidation<CreateLocation.Command>()
            .Produces<CreateLocation.Response>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(InventoryPermissions.LocationsEdit);
    }
}
