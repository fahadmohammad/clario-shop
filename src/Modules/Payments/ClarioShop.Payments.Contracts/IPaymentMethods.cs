namespace ClarioShop.Payments.Contracts;

/// <summary>Which ways of paying the current store accepts, for checkout to check before it places an order.</summary>
public interface IPaymentMethods
{
    /// <summary>
    /// True when the store takes payments this way now: cash on delivery always, a gateway (e.g. <c>SslCommerz</c>)
    /// only while its merchant account is set up and switched on.
    /// </summary>
    Task<bool> IsAvailableAsync(string paymentMethod, CancellationToken cancellationToken);
}
