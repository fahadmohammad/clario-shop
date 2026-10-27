using ClarioShop.Inventory.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClarioShop.Inventory.Persistence.Configurations;

internal sealed class StockLevelConfiguration : IEntityTypeConfiguration<StockLevel>
{
    public void Configure(EntityTypeBuilder<StockLevel> builder)
    {
        builder.ToTable("StockLevels");
        builder.HasKey(level => level.Id);
        builder.HasIndex(level => new { level.VariantId, level.LocationId }).IsUnique();
        builder.Property(level => level.OnHand).HasPrecision(18, 3);
        builder.Property(level => level.Reserved).HasPrecision(18, 3);
        builder.Ignore(level => level.Available);
        builder.Property(level => level.RowVersion).IsRowVersion();
        builder.HasOne<Location>().WithMany().HasForeignKey(level => level.LocationId).OnDelete(DeleteBehavior.Restrict);
    }
}
