using ClarioShop.Catalog.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClarioShop.Catalog.Persistence.Configurations;

internal sealed class AttributeValueConfiguration : IEntityTypeConfiguration<AttributeValue>
{
    public void Configure(EntityTypeBuilder<AttributeValue> builder)
    {
        builder.ToTable("AttributeValues");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).ValueGeneratedNever();
        builder.Property(value => value.Value).HasMaxLength(50);
        builder.Property(value => value.NormalizedValue).HasMaxLength(50);
        builder.HasIndex(value => new { value.AttributeId, value.NormalizedValue }).IsUnique();

        // Lets a variant's value row point at (value, attribute) together, so it cannot claim the wrong attribute.
        builder.HasAlternateKey(value => new { value.Id, value.AttributeId });
    }
}
