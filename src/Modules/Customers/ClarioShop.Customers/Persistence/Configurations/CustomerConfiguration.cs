using ClarioShop.Customers.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClarioShop.Customers.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(customer => customer.Id);
        builder.Property(customer => customer.Phone).HasMaxLength(16);
        builder.HasIndex(customer => customer.Phone).IsUnique();
        builder.Property(customer => customer.Name).HasMaxLength(200);
        builder.Property(customer => customer.Email).HasMaxLength(254);
        builder.HasIndex(customer => customer.Email);
        builder.Property(customer => customer.RowVersion).IsRowVersion();

        builder.HasMany(customer => customer.Addresses).WithOne().HasForeignKey(address => address.CustomerId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(customer => customer.Addresses).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
