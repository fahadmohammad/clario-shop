using ClarioShop.Ordering.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClarioShop.Ordering.Persistence.Configurations;

internal sealed class OrderLineConfiguration : IEntityTypeConfiguration<OrderLine>
{
    public void Configure(EntityTypeBuilder<OrderLine> builder)
    {
        builder.ToTable("OrderLines");
        builder.HasKey(line => line.Id);
        builder.Property(line => line.Id).ValueGeneratedNever();
        builder.Property(line => line.ProductName).HasMaxLength(200);
        builder.Property(line => line.VariantName).HasMaxLength(100);
        builder.Property(line => line.Sku).HasMaxLength(64);
        builder.Property(line => line.UnitPrice).HasPrecision(18, 2);
        builder.Property(line => line.Quantity).HasPrecision(18, 3);
        builder.Property(line => line.LineTotal).HasPrecision(18, 2);
        builder.HasIndex(line => new { line.OrderId, line.Position }).IsUnique();
        builder.HasIndex(line => line.VariantId);
    }
}
