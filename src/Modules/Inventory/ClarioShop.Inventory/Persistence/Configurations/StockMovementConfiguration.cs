using ClarioShop.Inventory.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClarioShop.Inventory.Persistence.Configurations;

internal sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements");
        builder.HasKey(movement => movement.Id);
        builder.Property(movement => movement.Quantity).HasPrecision(18, 3);
        builder.Property(movement => movement.Type).HasConversion<string>().HasMaxLength(30);
        builder.Property(movement => movement.Reason).HasConversion<string>().HasMaxLength(30);
        builder.Property(movement => movement.ReferenceType).HasMaxLength(50);
        builder.Property(movement => movement.UnitCost).HasPrecision(18, 2);
        builder.HasIndex(movement => new { movement.VariantId, movement.LocationId, movement.RecordedAt });
        builder.HasIndex(movement => new { movement.ReferenceType, movement.ReferenceId });
        builder.HasOne<Location>().WithMany().HasForeignKey(movement => movement.LocationId).OnDelete(DeleteBehavior.Restrict);
    }
}
