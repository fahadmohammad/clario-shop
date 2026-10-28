namespace ClarioShop.Payments.Domain;

internal enum AttemptOutcome
{
    /// <summary>A gateway session is open and the gateway has not confirmed a payment yet.</summary>
    Pending,

    Succeeded,

    Failed,
}
