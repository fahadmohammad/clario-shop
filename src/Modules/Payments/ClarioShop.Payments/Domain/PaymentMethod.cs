namespace ClarioShop.Payments.Domain;

/// <summary>How the money arrives. Gateways (SSLCommerz, bKash) follow with prepaid checkout.</summary>
internal enum PaymentMethod
{
    CashOnDelivery,
}
