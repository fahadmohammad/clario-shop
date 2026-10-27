using ClarioShop.Inventory.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClarioShop.Inventory.Persistence.Configurations;

internal sealed class StockAdjustmentConfiguration : IEntityTypeConfiguration<StockAdjustment>
{
    public void Configure(EntityTypeBuilder<StockAdjustment> builder)
    {
        builder.ToTable("StockAdjustments");
        builder.HasKey(adjustment => adjustment.Id);
        builder.Property(adjustment => adjustment.Reason).HasConversion<string>().HasMaxLength(30);
        builder.Property(adjustment => adjustment.Note).HasMaxLength(1000);
        builder.HasIndex(adjustment => adjustment.RecordedAt);
        builder.HasOne<Location>().WithMany().HasForeignKey(adjustment => adjustment.LocationId).OnDelete(DeleteBehavior.Restrict);

        builder.OwnsMany(adjustment => adjustment.Lines, line =>
        {
            line.ToTable("StockAdjustmentLines");
            line.WithOwner().HasForeignKey(l => l.AdjustmentId);
            line.HasKey(l => l.Id);
            line.Property(l => l.QuantityChange).HasPrecision(18, 3);
        });
        builder.Navigation(adjustment => adjustment.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
