using System.Security.Claims;
using ClarioShop.Ordering.Features.Orders;
using ClarioShop.SharedKernel.Tenancy;

namespace ClarioShop.Ordering.Features.Checkout;

internal sealed class CheckoutEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/ordering/checkout", static async (Checkout.Request request, ClaimsPrincipal user, ITenantContext tenant, Checkout.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new Checkout.Command(request, user.ShopperCustomerId(tenant)), cancellationToken)).ToHttpResult(placed => placed.Created
                    ? TypedResults.Created($"/api/ordering/orders/{placed.Order.Id}", placed.Order)
                    : TypedResults.Ok(placed.Order)))
            .WithTags("Ordering")
            .WithName("Checkout")
            .WithSummary("Place a cash-on-delivery order from a cart, as a guest or a signed-in shopper (who may use a saved address). Repeating it for the same cart returns the same order (200).")
            .WithValidation<Checkout.Request>()
            .Produces<OrderDetails>(StatusCodes.Status201Created)
            .Produces<OrderDetails>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .AllowAnonymous();
    }
}
