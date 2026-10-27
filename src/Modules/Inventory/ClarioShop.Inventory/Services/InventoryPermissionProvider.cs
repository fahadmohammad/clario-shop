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
        new(InventoryPermissions.LocationsEdit, "inventory", "Create and edit stock locations"),
    ];
}
