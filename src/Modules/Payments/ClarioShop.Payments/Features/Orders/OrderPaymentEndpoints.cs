using System.Security.Claims;
using ClarioShop.Infrastructure.Tenancy;
using ClarioShop.Payments.Contracts;
using ClarioShop.Payments.Services;
using ClarioShop.SharedKernel.Tenancy;

namespace ClarioShop.Payments.Features.Orders;

internal sealed class OrderPaymentEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payments/orders").WithTags("Payments").RequirePlanFeature(PlanFeatures.OnlineStore);

        group.MapGet("/{orderId:guid}", static async (Guid orderId, GetOrderPayments.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new GetOrderPayments.Query(orderId), cancellationToken)).ToHttpResult())
            .WithName("GetOrderPayments")
            .WithSummary("An order's payments with their attempts, the amount paid and what is still owed.")
            .Produces<OrderPayments>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequirePermission(PaymentsPermissions.Manage);

        group.MapPost("/{orderId:guid}/cash-collections", static async (Guid orderId, RecordCashCollection.Request request, ClaimsPrincipal user, RecordCashCollection.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new RecordCashCollection.Command(orderId, request.Amount, request.Note, user.UserId()), cancellationToken))
                    .ToHttpResult(payments => TypedResults.Created($"/api/payments/orders/{orderId}", payments)))
            .WithName("RecordCashCollection")
            .WithSummary("Record cash collected on delivery for a shipped or delivered order. Fails with 409 when it is more than is still owed.")
            .WithValidation<RecordCashCollection.Request>()
            .Produces<OrderPayments>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(PaymentsPermissions.Manage);
    }
}
