using ClarioShop.Catalog.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClarioShop.Catalog.Persistence.Configurations;

internal sealed class VariantConfiguration : IEntityTypeConfiguration<Variant>
{
    public void Configure(EntityTypeBuilder<Variant> builder)
    {
        builder.ToTable("Variants");
        builder.HasKey(variant => variant.Id);
        builder.Property(variant => variant.Id).ValueGeneratedNever();
        builder.Property(variant => variant.Sku).HasMaxLength(64);
        builder.HasIndex(variant => variant.Sku).IsUnique();
        builder.Property(variant => variant.Name).HasMaxLength(100);
        builder.Property(variant => variant.Price).HasPrecision(18, 2);
        builder.Property(variant => variant.CompareAtPrice).HasPrecision(18, 2);
        builder.Property(variant => variant.Barcodes).HasColumnType("text[]");
        builder.HasIndex(variant => variant.Barcodes).HasMethod("gin");
        builder.Property(variant => variant.RowVersion).IsRowVersion();
    }
}
