using ClarioShop.Catalog.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClarioShop.Catalog.Persistence.Configurations;

internal sealed class ProductAttributeConfiguration : IEntityTypeConfiguration<ProductAttribute>
{
    public void Configure(EntityTypeBuilder<ProductAttribute> builder)
    {
        builder.ToTable("Attributes");
        builder.HasKey(attribute => attribute.Id);
        builder.Property(attribute => attribute.Name).HasMaxLength(50);
        builder.Property(attribute => attribute.NormalizedName).HasMaxLength(50);
        builder.HasIndex(attribute => attribute.NormalizedName).IsUnique();

        builder.HasMany(attribute => attribute.Values).WithOne().HasForeignKey(value => value.AttributeId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(attribute => attribute.Values).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
