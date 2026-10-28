namespace ClarioShop.Ordering.Domain;

/// <summary>The readable order number shoppers and staff quote, from the tenant's order sequence.</summary>
internal static class OrderNumber
{
    public const string Prefix = "CS-";

    public static string Format(long number) => $"{Prefix}{number:D6}";
}
