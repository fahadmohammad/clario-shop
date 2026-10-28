namespace ClarioShop.Ordering.Domain;

internal static class Money
{
    /// <summary>Rounds to whole poisha, half away from zero (৳12.345 → ৳12.35), as shoppers expect on a receipt.</summary>
    public static decimal Round(decimal amount) => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
}
