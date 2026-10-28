namespace ClarioShop.Payments.Gateways;

/// <summary>
/// A hosted payment page provider such as SSLCommerz: the shopper pays on the gateway's page, and the result is only
/// trusted once <see cref="ValidateAsync"/> confirms it with the gateway itself.
/// </summary>
internal interface IPaymentGateway
{
    /// <summary>E.g. <c>SslCommerz</c>.</summary>
    string Name { get; }

    /// <summary>Opens a payment session; the shopper is sent to <see cref="GatewaySession.RedirectUrl"/> to pay.</summary>
    Task<Result<GatewaySession>> CreateSessionAsync(GatewaySessionRequest request, CancellationToken cancellationToken);

    /// <summary>Asks the gateway what a transaction it reported (by IPN or redirect) really is.</summary>
    /// <param name="validationId">The gateway's id for the reported transaction (SSLCommerz <c>val_id</c>).</param>
    Task<Result<GatewayTransaction>> ValidateAsync(string validationId, CancellationToken cancellationToken);
}
