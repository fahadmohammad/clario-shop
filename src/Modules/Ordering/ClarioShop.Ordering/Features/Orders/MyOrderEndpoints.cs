using System.Security.Claims;
using ClarioShop.SharedKernel.Tenancy;

namespace ClarioShop.Ordering.Features.Orders;

/// <summary>The signed-in shopper's order history. The customer always comes from the token, never from the request.</summary>
internal sealed class MyOrderEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ordering/me/orders").WithTags("My account").RequireShopper();

        group.MapGet("/", static async ([AsParameters] ListMyOrders.Request request, ClaimsPrincipal user, ITenantContext tenant, ListMyOrders.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new ListMyOrders.Query(Me(user, tenant), request.Page, request.PageSize), cancellationToken)).ToHttpResult())
            .WithName("ListMyOrders")
            .WithSummary("The signed-in shopper's orders, newest first.")
            .WithValidation<ListMyOrders.Request>()
            .Produces<ListMyOrders.Response>();

        group.MapGet("/{id:guid}", static async (Guid id, ClaimsPrincipal user, ITenantContext tenant, GetMyOrder.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new GetMyOrder.Query(Me(user, tenant), id), cancellationToken)).ToHttpResult())
            .WithName("GetMyOrder")
            .WithSummary("One of the signed-in shopper's orders with its lines, charges and status history.")
            .Produces<OrderDetails>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    /// <summary>The shopper's customer id; the group's shopper policy guarantees there is one.</summary>
    private static Guid Me(ClaimsPrincipal user, ITenantContext tenant) =>
        user.ShopperCustomerId(tenant) ?? throw new InvalidOperationException("Shopper endpoint reached without a shopper.");
}
