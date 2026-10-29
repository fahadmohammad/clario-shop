using ClarioShop.Inventory.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClarioShop.Inventory.Persistence.Configurations;

internal sealed class StockDriftConfiguration : IEntityTypeConfiguration<StockDrift>
{
    public void Configure(EntityTypeBuilder<StockDrift> builder)
    {
        builder.ToTable("StockDrifts");
        builder.HasKey(drift => drift.Id);
        builder.Property(drift => drift.RecordedOnHand).HasPrecision(18, 3);
        builder.Property(drift => drift.LedgerOnHand).HasPrecision(18, 3);
        builder.Property(drift => drift.RecordedReserved).HasPrecision(18, 3);
        builder.Property(drift => drift.HeldReserved).HasPrecision(18, 3);
        builder.HasIndex(drift => drift.DetectedAt);
        builder.HasOne<Location>().WithMany().HasForeignKey(drift => drift.LocationId).OnDelete(DeleteBehavior.Restrict);
    }
}
