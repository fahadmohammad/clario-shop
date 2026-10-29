namespace ClarioShop.Catalog.Domain;

/// <summary>Actions Catalog writes to its audit log.</summary>
internal static class AuditActions
{
    public const string PriceChanged = "catalog.price.changed";
}
