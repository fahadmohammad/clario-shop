using ClarioShop.Catalog.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClarioShop.Catalog.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(product => product.Id);
        builder.Property(product => product.Name).HasMaxLength(200);
        builder.Property(product => product.Slug).HasMaxLength(160);
        builder.HasIndex(product => product.Slug).IsUnique();
        builder.Property(product => product.Description).HasMaxLength(10_000);
        builder.Property(product => product.Brand).HasMaxLength(100);
        builder.Property(product => product.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(product => new { product.Status, product.CategoryId });
        builder.Property(product => product.RowVersion).IsRowVersion();
        builder.HasOne<Category>().WithMany().HasForeignKey(product => product.CategoryId).OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(product => product.Variants).WithOne().HasForeignKey(variant => variant.ProductId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(product => product.Variants).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
