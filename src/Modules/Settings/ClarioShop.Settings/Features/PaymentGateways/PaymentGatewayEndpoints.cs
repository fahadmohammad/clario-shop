using ClarioShop.Settings.Contracts;

namespace ClarioShop.Settings.Features.PaymentGateways;

internal sealed class PaymentGatewayEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/settings/payment-gateways").WithTags("Settings");

        group.MapGet("/", static async (ListPaymentGateways.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new ListPaymentGateways.Query(), cancellationToken)).ToHttpResult())
            .WithName("ListPaymentGateways")
            .WithSummary("The store's payment gateway accounts, switched off ones included. Secrets are never returned.")
            .Produces<IReadOnlyList<PaymentGatewayResponse>>()
            .RequirePermission(SettingsPermissions.Edit);

        group.MapPut("/sslcommerz", static async (UpdateSslCommerz.Command command, UpdateSslCommerz.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(command, cancellationToken)).ToHttpResult())
            .WithName("UpdateSslCommerz")
            .WithSummary("Save the SSLCommerz merchant account: store id, store password (required the first time; leave it out to keep the saved one), sandbox or live, on or off.")
            .WithValidation<UpdateSslCommerz.Command>()
            .Produces<PaymentGatewayResponse>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(SettingsPermissions.Edit);
    }
}
