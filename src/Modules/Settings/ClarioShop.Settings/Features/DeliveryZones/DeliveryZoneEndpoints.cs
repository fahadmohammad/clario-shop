using ClarioShop.Settings.Contracts;

namespace ClarioShop.Settings.Features.DeliveryZones;

internal sealed class DeliveryZoneEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/settings/delivery-zones").WithTags("Settings");

        group.MapGet("/", static async (ListDeliveryZones.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new ListDeliveryZones.Query(), cancellationToken)).ToHttpResult())
            .WithName("ListDeliveryZones")
            .WithSummary("Every delivery zone, inactive ones included, in display order.")
            .Produces<IReadOnlyList<DeliveryZoneResponse>>()
            .RequirePermission(SettingsPermissions.Edit);

        group.MapPost("/", static async (DeliveryZoneInput zone, CreateDeliveryZone.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(zone, cancellationToken))
                    .ToHttpResult(created => TypedResults.Created($"/api/settings/delivery-zones/{created.Id}", created)))
            .WithName("CreateDeliveryZone")
            .WithSummary("Add a delivery zone with its areas, fee and optional free-delivery threshold. Names are unique ignoring case.")
            .WithValidation<DeliveryZoneInput>()
            .Produces<DeliveryZoneResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(SettingsPermissions.Edit);

        group.MapPut("/{id:guid}", static async (Guid id, DeliveryZoneInput zone, UpdateDeliveryZone.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new UpdateDeliveryZone.Command(id, zone), cancellationToken)).ToHttpResult())
            .WithName("UpdateDeliveryZone")
            .WithSummary("Replace a delivery zone. Set isActive false to stop offering it without deleting it.")
            .WithValidation<DeliveryZoneInput>()
            .Produces<DeliveryZoneResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(SettingsPermissions.Edit);

        group.MapDelete("/{id:guid}", static async (Guid id, DeleteDeliveryZone.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new DeleteDeliveryZone.Command(id), cancellationToken)).ToHttpResult(_ => TypedResults.NoContent()))
            .WithName("DeleteDeliveryZone")
            .WithSummary("Delete a delivery zone. Placed orders keep the fee they were charged.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequirePermission(SettingsPermissions.Edit);
    }
}
