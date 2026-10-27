using ClarioShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Design;

namespace ClarioShop.Inventory.Persistence;

internal sealed class InventoryDesignTimeFactory : IDesignTimeDbContextFactory<InventoryDbContext>
{
    public InventoryDbContext CreateDbContext(string[] args) =>
        new(TenantDbContextRegistration.DesignTimeOptions<InventoryDbContext>(InventoryDbContext.SchemaName));
}
