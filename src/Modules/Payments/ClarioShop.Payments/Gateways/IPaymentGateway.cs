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

    /// <summary>
    /// Asks the gateway about a transaction id we opened a session for, whether or not it ever called back: every try
    /// the shopper made for it, none when they never got to pay.
    /// </summary>
    /// <param name="transactionId">Our id sent when the session was opened (SSLCommerz <c>tran_id</c>).</param>
    Task<Result<IReadOnlyList<GatewayAttempt>>> FindAttemptsAsync(string transactionId, CancellationToken cancellationToken);
}
