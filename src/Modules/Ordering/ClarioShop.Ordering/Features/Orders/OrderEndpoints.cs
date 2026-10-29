using System.Security.Claims;
using ClarioShop.Infrastructure.Tenancy;
using ClarioShop.Ordering.Contracts;
using ClarioShop.Ordering.Domain;
using ClarioShop.Ordering.Services;
using ClarioShop.SharedKernel.Tenancy;

namespace ClarioShop.Ordering.Features.Orders;

internal sealed class OrderEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ordering/orders").WithTags("Ordering").RequirePlanFeature(PlanFeatures.OnlineStore);

        group.MapGet("/", static async ([AsParameters] ListOrders.Query query, ListOrders.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(query, cancellationToken)).ToHttpResult())
            .WithName("ListOrders")
            .WithSummary("Orders, newest first, optionally only those in one status (e.g. Placed, to confirm).")
            .WithValidation<ListOrders.Query>()
            .Produces<ListOrders.Response>()
            .RequirePermission(OrderingPermissions.OrdersView);

        group.MapGet("/{id:guid}", static async (Guid id, GetOrder.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new GetOrder.Query(id), cancellationToken)).ToHttpResult())
            .WithName("GetOrder")
            .WithSummary("An order with its lines as priced at checkout and its status history.")
            .Produces<OrderDetails>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequirePermission(OrderingPermissions.OrdersView);

        group.MapPost("/{id:guid}/confirm", static async (Guid id, StatusChangeRequest? request, ClaimsPrincipal user, ConfirmOrder.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new ConfirmOrder.Command(id, request?.Note, user.UserId()), cancellationToken)).ToHttpResult())
            .WithName("ConfirmOrder")
            .WithSummary("Confirm a placed order; its stock is sold. Fails with 409 when the stock hold expired and the stock is gone.")
            .WithValidation<StatusChangeRequest>()
            .Produces<OrderDetails>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(OrderingPermissions.OrdersManage);

        MapStatusChange(group, "pack", OrderStatus.Packed, "Mark a confirmed order as packed.");
        MapStatusChange(group, "ship", OrderStatus.Shipped, "Hand a packed order to the courier or rider.");
        MapStatusChange(group, "deliver", OrderStatus.Delivered, "Record that the shopper received a shipped order.");
        MapStatusChange(group, "cancel", OrderStatus.Cancelled, "Cancel an order that has not shipped; its stock goes back.");
        MapStatusChange(group, "return", OrderStatus.Returned, "Record that a shipped or delivered order came back.");
    }

    private static void MapStatusChange(RouteGroupBuilder group, string action, OrderStatus target, string summary) =>
        group.MapPost($"/{{id:guid}}/{action}", async (Guid id, StatusChangeRequest? request, ClaimsPrincipal user, ChangeOrderStatus.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new ChangeOrderStatus.Command(id, target, request?.Note, user.UserId()), cancellationToken)).ToHttpResult())
            .WithName($"{char.ToUpperInvariant(action[0])}{action[1..]}Order")
            .WithSummary(summary)
            .WithValidation<StatusChangeRequest>()
            .Produces<OrderDetails>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(OrderingPermissions.OrdersManage);
}
