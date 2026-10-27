namespace ClarioShop.Identity.Contracts;

public static class SystemRoles
{
    /// <summary>Holds every permission; created for each tenant and kept in sync on startup.</summary>
    public const string Owner = "Owner";

    public const string CatalogManager = "Catalog Manager";

    public const string InventoryClerk = "Inventory Clerk";
}
