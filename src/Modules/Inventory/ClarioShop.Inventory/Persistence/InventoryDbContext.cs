using ClarioShop.Infrastructure.Auditing;
using ClarioShop.Infrastructure.Persistence;
using ClarioShop.Inventory.Contracts.Events;
using ClarioShop.Inventory.Domain;

namespace ClarioShop.Inventory.Persistence;

internal sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options, TimeProvider clock) : ModuleDbContext(options)
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

    public DbSet<StockTransfer> Transfers => Set<StockTransfer>();

    public DbSet<StockDrift> Drifts => Set<StockDrift>();

    public DbSet<AuditLogEntry> AuditLog => Set<AuditLogEntry>();

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        await PublishStockWentNegativeAsync(cancellationToken);
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.MapAuditLog(Schema);
    }

    /// <summary>
    /// Every path that moves stock saves through here, so any stock level about to cross below zero (a sale, a
    /// transfer, a drift correction) raises <see cref="StockWentNegative"/> in the same transaction.
    /// </summary>
    private async Task PublishStockWentNegativeAsync(CancellationToken cancellationToken)
    {
        var crossed = ChangeTracker.Entries<StockLevel>()
            .Where(entry => entry.Entity.OnHand < 0 && entry.State switch
            {
                EntityState.Added => true,
                EntityState.Modified => entry.Property(level => level.OnHand).OriginalValue >= 0,
                _ => false,
            })
            .Select(entry => entry.Entity)
            .ToList();
        if (crossed.Count == 0)
        {
            return;
        }

        var variantIds = crossed.Select(level => level.VariantId).Distinct().ToList();
        var locationIds = crossed.Select(level => level.LocationId).Distinct().ToList();
        var items = await Items.AsNoTracking()
            .Where(item => variantIds.Contains(item.VariantId))
            .ToDictionaryAsync(item => item.VariantId, item => (item.Sku, item.ProductName), cancellationToken);
        var locations = await Locations.AsNoTracking()
            .Where(location => locationIds.Contains(location.Id))
            .ToDictionaryAsync(location => location.Id, location => location.Code, cancellationToken);

        var now = clock.GetUtcNow();
        foreach (var level in crossed)
        {
            var (sku, productName) = items.GetValueOrDefault(level.VariantId, (string.Empty, string.Empty));
            Publish(new StockWentNegative(
                Guid.CreateVersion7(),
                now,
                level.VariantId,
                sku,
                productName,
                level.LocationId,
                locations.GetValueOrDefault(level.LocationId, string.Empty),
                level.OnHand));
        }
    }
}
