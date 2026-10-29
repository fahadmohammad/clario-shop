using ClarioShop.Inventory.Contracts;
using ClarioShop.SharedKernel.Security;

namespace ClarioShop.Inventory.Services;

internal sealed class InventoryPermissionProvider : IPermissionProvider
{
    public IReadOnlyCollection<PermissionDefinition> GetPermissions() =>
    [
        new(InventoryPermissions.StockView, "inventory", "View stock levels, locations and the stock ledger"),
        new(InventoryPermissions.Receive, "inventory", "Record goods received from suppliers"),
        new(InventoryPermissions.Adjust, "inventory", "Adjust stock for damage, expiry, theft or count corrections"),
        new(InventoryPermissions.Transfer, "inventory", "Transfer stock between locations"),
        new(InventoryPermissions.LocationsEdit, "inventory", "Create and edit stock locations"),

        // Kept out of the "inventory" group so only the Owner gets it, not the Inventory Clerk whose actions it shows.
        new(InventoryPermissions.AuditView, "audit", "View the audit log of stock adjustments"),
    ];
}
