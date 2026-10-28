namespace ClarioShop.Payments.Domain;

/// <summary>How the money arrives. Names match the order's payment method in Ordering.</summary>
internal enum PaymentMethod
{
    CashOnDelivery,

    /// <summary>Paid on SSLCommerz's hosted payment page.</summary>
    SslCommerz,
}
