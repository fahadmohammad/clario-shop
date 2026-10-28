namespace ClarioShop.Payments.Gateways;

/// <summary>What the gateway needs to open a payment session for one payment.</summary>
/// <param name="TransactionId">Our id for the payment (SSLCommerz <c>tran_id</c>); the gateway reports it back.</param>
/// <param name="Currency">ISO 4217, e.g. <c>BDT</c>.</param>
/// <param name="OrderNumber">Shown to staff in the gateway's merchant panel.</param>
/// <param name="ItemCount">Number of order lines.</param>
internal sealed record GatewaySessionRequest(
    string TransactionId,
    decimal Amount,
    string Currency,
    string OrderNumber,
    int ItemCount,
    GatewayCustomer Customer,
    GatewayCallbacks Callbacks);

/// <param name="Phone">E.164, e.g. +8801712345678.</param>
/// <param name="Email">The gateway requires one; the caller supplies a fallback when the shopper has none.</param>
internal sealed record GatewayCustomer(string Name, string Phone, string Email, string AddressLine, string City, string? PostalCode);

/// <summary>
/// Where the gateway sends the shopper's browser after paying, failing or cancelling, and where it posts the
/// server-to-server notification (IPN).
/// </summary>
internal sealed record GatewayCallbacks(Uri Success, Uri Fail, Uri Cancel, Uri Ipn);
