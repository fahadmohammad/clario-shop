namespace ClarioShop.Ordering.Domain;

/// <summary>How the shopper pays. Names match <see cref="ClarioShop.Ordering.Contracts.OrderPaymentMethods"/>.</summary>
internal enum PaymentMethod
{
    CashOnDelivery,

    /// <summary>Paid up front on SSLCommerz's hosted payment page.</summary>
    SslCommerz,
}

internal static class PaymentMethodExtensions
{
    /// <summary>Paid before the order is confirmed, through a gateway, rather than in cash on delivery.</summary>
    public static bool IsPrepaid(this PaymentMethod method) => method != PaymentMethod.CashOnDelivery;
}
