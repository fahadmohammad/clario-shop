using ClarioShop.Customers.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClarioShop.Customers.Persistence.Configurations;

internal sealed class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        // At most one default address per customer is enforced by a deferred exclusion constraint added in the
        // migration (a unique index would reject the moment between unsetting the old default and setting the new one).
        builder.ToTable("Addresses");
        builder.HasKey(address => address.Id);

        // Ids come from Guid.CreateVersion7(); without this, an address added to a loaded customer is taken for an existing row.
        builder.Property(address => address.Id).ValueGeneratedNever();
        builder.Property(address => address.Label).HasMaxLength(50);
        builder.Property(address => address.RecipientName).HasMaxLength(200);
        builder.Property(address => address.RecipientPhone).HasMaxLength(16);
        builder.Property(address => address.Line1).HasMaxLength(300);
        builder.Property(address => address.Line2).HasMaxLength(300);
        builder.Property(address => address.Area).HasMaxLength(100);
        builder.Property(address => address.City).HasMaxLength(100);
        builder.Property(address => address.PostalCode).HasMaxLength(10);
    }
}
