using ClarioShop.Ordering.Contracts;

namespace ClarioShop.Ordering.Features.Orders;

internal sealed class OrderEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ordering/orders").WithTags("Ordering");

        group.MapGet("/{id:guid}", static async (Guid id, GetOrder.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new GetOrder.Query(id), cancellationToken)).ToHttpResult())
            .WithName("GetOrder")
            .WithSummary("An order with its lines as priced at checkout and its status history.")
            .Produces<OrderDetails>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequirePermission(OrderingPermissions.OrdersView);
    }
}
