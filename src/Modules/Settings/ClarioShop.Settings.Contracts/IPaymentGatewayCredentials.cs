namespace ClarioShop.Settings.Contracts;

/// <summary>
/// The current tenant's merchant credentials for the payment gateways, decrypted, for the Payments module only.
/// Never cached, so secrets stay out of Redis, and never to be logged or returned to a client.
/// </summary>
public interface IPaymentGatewayCredentials
{
    /// <summary>The store's SSLCommerz account; null when it has none or it is switched off.</summary>
    Task<SslCommerzCredentials?> GetSslCommerzAsync(CancellationToken cancellationToken);
}
