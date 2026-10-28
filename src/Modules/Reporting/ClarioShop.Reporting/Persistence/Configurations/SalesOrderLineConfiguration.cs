using ClarioShop.Reporting.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClarioShop.Reporting.Persistence.Configurations;

internal sealed class SalesOrderLineConfiguration : IEntityTypeConfiguration<SalesOrderLine>
{
    public void Configure(EntityTypeBuilder<SalesOrderLine> builder)
    {
        builder.ToTable("SalesOrderLines");
        builder.HasKey(line => new { line.OrderId, line.Position });
        builder.Property(line => line.Position).ValueGeneratedNever();
        builder.HasIndex(line => line.VariantId);

        builder.Property(line => line.Sku).HasMaxLength(64);
        builder.Property(line => line.ProductName).HasMaxLength(200);
        builder.Property(line => line.VariantName).HasMaxLength(100);
        builder.Property(line => line.Quantity).HasPrecision(18, 3);
        builder.Property(line => line.UnitPrice).HasPrecision(18, 2);
        builder.Property(line => line.LineTotal).HasPrecision(18, 2);
    }
}
