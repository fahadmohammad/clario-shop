using ClarioShop.Ordering.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClarioShop.Ordering.Persistence.Configurations;

internal sealed class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable("CartItems");
        builder.HasKey(item => item.Id);

        // Ids come from Guid.CreateVersion7(); without this, an item added to a loaded cart is taken for an existing row.
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Quantity).HasPrecision(18, 3);
        builder.HasIndex(item => new { item.CartId, item.VariantId }).IsUnique();
    }
}
