using ClarioShop.Catalog.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClarioShop.Catalog.Persistence.Configurations;

internal sealed class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.ToTable("Brands");
        builder.HasKey(brand => brand.Id);
        builder.Property(brand => brand.Name).HasMaxLength(100);
        builder.Property(brand => brand.NormalizedName).HasMaxLength(100);
        builder.HasIndex(brand => brand.NormalizedName).IsUnique();
        builder.Property(brand => brand.Slug).HasMaxLength(160);
        builder.HasIndex(brand => brand.Slug).IsUnique();
    }
}
