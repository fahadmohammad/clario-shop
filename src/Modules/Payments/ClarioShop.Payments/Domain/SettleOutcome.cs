namespace ClarioShop.Payments.Domain;

internal enum SettleOutcome
{
    /// <summary>The payment succeeded just now.</summary>
    Settled,

    /// <summary>This attempt had already settled the payment, e.g. the IPN after the shopper's redirect.</summary>
    AlreadySettled,

    /// <summary>The shopper paid on a second session after the first had already paid; the extra money needs a refund.</summary>
    PaidTwice,
}
