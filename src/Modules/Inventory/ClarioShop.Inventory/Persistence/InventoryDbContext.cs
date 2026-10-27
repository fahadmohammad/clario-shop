using ClarioShop.Infrastructure.Persistence;
using ClarioShop.Inventory.Domain;

namespace ClarioShop.Inventory.Persistence;

internal sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "inventory";

    public override string Schema => SchemaName;

    public DbSet<Location> Locations => Set<Location>();

    public DbSet<InventoryItem> Items => Set<InventoryItem>();

    public DbSet<StockMovement> Movements => Set<StockMovement>();

    public DbSet<StockLevel> StockLevels => Set<StockLevel>();

    public DbSet<StockReservation> Reservations => Set<StockReservation>();

    public DbSet<PurchaseReceipt> PurchaseReceipts => Set<PurchaseReceipt>();

    public DbSet<StockAdjustment> Adjustments => Set<StockAdjustment>();
}
