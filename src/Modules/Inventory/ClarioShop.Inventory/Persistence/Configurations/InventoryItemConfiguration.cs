using ClarioShop.Inventory.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClarioShop.Inventory.Persistence.Configurations;

internal sealed class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.ToTable("Items");
        builder.HasKey(item => item.VariantId);
        builder.Property(item => item.VariantId).ValueGeneratedNever();
        builder.Property(item => item.Sku).HasMaxLength(64);
        builder.HasIndex(item => item.Sku);
        builder.Property(item => item.ProductName).HasMaxLength(200);
        builder.Property(item => item.VariantName).HasMaxLength(100);
        builder.HasIndex(item => item.ProductId);
    }
}
