using ClarioShop.Catalog.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClarioShop.Catalog.Persistence.Configurations;

internal sealed class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("ProductImages");
        builder.HasKey(image => image.Id);
        builder.Property(image => image.Id).ValueGeneratedNever();
        builder.Property(image => image.StorageKey).HasMaxLength(300);
        builder.HasIndex(image => image.StorageKey).IsUnique();
        builder.Property(image => image.ContentType).HasMaxLength(50);
        builder.Property(image => image.AltText).HasMaxLength(200);
        builder.HasIndex(image => new { image.ProductId, image.Position });
    }
}
