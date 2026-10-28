using ClarioShop.Payments.Domain;
using ClarioShop.Payments.Gateways.SslCommerz;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

namespace ClarioShop.Payments.Features.Gateways;

internal sealed class GatewayEndpoints : IEndpointGroup
{
    /// <summary>
    /// Gateway callbacks are a few dozen short fields; anything much bigger is not from the gateway, and every callback
    /// is stored, so the anonymous endpoints refuse it.
    /// </summary>
    private static readonly FormOptions CallbackFormLimits = new()
    {
        BufferBodyLengthLimit = 64 * 1024,
        MultipartBodyLengthLimit = 64 * 1024,
        ValueCountLimit = 200,
        KeyLengthLimit = 100,
        ValueLengthLimit = 4 * 1024,
    };

    public void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/payments/orders/{orderId:guid}/gateway-sessions", static async (Guid orderId, HttpRequest request, StartGatewayPayment.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new StartGatewayPayment.Command(orderId, CallbackBase(request)), cancellationToken))
                    .ToHttpResult(started => TypedResults.Created($"/api/payments/orders/{orderId}", started)))
            .WithTags("Payments")
            .WithName("StartGatewayPayment")
            .WithSummary("Open a payment session on the gateway for a prepaid order waiting for its payment; send the shopper to redirectUrl. Call it again to retry.")
            .Produces<StartGatewayPayment.Response>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .AllowAnonymous();

        // SSLCommerz posts form fields to these. The gateway and the shopper's browser are not signed in; nothing is
        // trusted until the gateway's validation API confirms it.
        var sslCommerz = app.MapGroup(GatewayCallbackRoutes.BasePath(SslCommerzGateway.GatewayName))
            .WithTags("Payments")
            .AllowAnonymous();

        sslCommerz.MapPost($"/{GatewayCallbackRoutes.Ipn}", static async (HttpRequest request, GatewayCallbackProcessor processor, CancellationToken cancellationToken) =>
            {
                if (await FieldsAsync(request, cancellationToken) is not { } fields)
                {
                    return Results.BadRequest();
                }

                var result = await processor.ProcessAsync(GatewayCallbackSource.Ipn, fields, cancellationToken);

                // Unavailable asks the gateway to send the notification again; anything else is handled for good.
                return result.Outcome == GatewayCallbackOutcome.GatewayUnavailable
                    ? Results.StatusCode(StatusCodes.Status503ServiceUnavailable)
                    : Results.Ok();
            })
            .WithName("SslCommerzIpn")
            .WithSummary("SSLCommerz's payment notification (IPN), form-encoded. Stored raw; a payment is settled only once the validation API confirms it. 503 asks SSLCommerz to retry.")
            .ProducesProblem(StatusCodes.Status400BadRequest);

        MapReturn(sslCommerz, GatewayCallbackRoutes.Success, GatewayCallbackSource.SuccessRedirect);
        MapReturn(sslCommerz, GatewayCallbackRoutes.Fail, GatewayCallbackSource.FailRedirect);
        MapReturn(sslCommerz, GatewayCallbackRoutes.Cancel, GatewayCallbackSource.CancelRedirect);
    }

    /// <summary>
    /// The shopper's browser coming back from the payment page: settles a confirmed payment at once (the IPN may come
    /// later), then sends the browser on to the storefront's payment result page on the same host.
    /// </summary>
    private static void MapReturn(RouteGroupBuilder group, string route, GatewayCallbackSource source) =>
        group.MapPost($"/{route}", async (HttpRequest request, HttpResponse response, GatewayCallbackProcessor processor, IOptions<SslCommerzOptions> options, CancellationToken cancellationToken) =>
            {
                if (await FieldsAsync(request, cancellationToken) is not { } fields)
                {
                    return Results.BadRequest();
                }

                var result = await processor.ProcessAsync(source, fields, cancellationToken);
                var payment = (source, result.Outcome) switch
                {
                    (GatewayCallbackSource.SuccessRedirect, GatewayCallbackOutcome.Settled or GatewayCallbackOutcome.AlreadySettled or GatewayCallbackOutcome.PaidTwice) => "paid",
                    (GatewayCallbackSource.SuccessRedirect, _) => "pending",
                    (GatewayCallbackSource.FailRedirect, _) => "failed",
                    _ => "cancelled",
                };
                var query = result.OrderId is { } orderId ? $"?orderId={orderId}&payment={payment}" : $"?payment={payment}";

                // 303 so the browser follows with a GET, not by posting the gateway's form on to the storefront.
                response.Headers.Location = $"{options.Value.ShopperReturnPath}{query}";
                return Results.StatusCode(StatusCodes.Status303SeeOther);
            })
            .WithName($"SslCommerz{char.ToUpperInvariant(route[0])}{route[1..]}Return")
            .WithSummary($"Where SSLCommerz sends the shopper's browser ({route}); redirects (303) to the storefront's payment result page.")
            .Produces(StatusCodes.Status303SeeOther);

    private static Uri CallbackBase(HttpRequest request) => new($"{request.Scheme}://{request.Host}/");

    /// <summary>The posted form fields; null when the body is not a form within <see cref="CallbackFormLimits"/>.</summary>
    private static async Task<IReadOnlyDictionary<string, string>?> FieldsAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType || request.ContentLength > CallbackFormLimits.BufferBodyLengthLimit)
        {
            return null;
        }

        try
        {
            var form = await request.ReadFormAsync(CallbackFormLimits, cancellationToken);
            return form.ToDictionary(field => field.Key, field => field.Value.ToString(), StringComparer.Ordinal);
        }
        catch (InvalidDataException)
        {
            // Over a form limit: the anonymous caller gets a 400, not a server error.
            return null;
        }
    }
}
