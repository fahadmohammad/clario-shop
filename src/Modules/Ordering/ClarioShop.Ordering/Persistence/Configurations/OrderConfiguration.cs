using ClarioShop.Ordering.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClarioShop.Ordering.Persistence.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");
        builder.HasKey(order => order.Id);
        builder.HasIndex(order => order.Number).IsUnique();

        // One order per cart: concurrent checkouts of the same cart cannot both succeed.
        builder.HasIndex(order => order.CartId).IsUnique();
        builder.HasIndex(order => order.CustomerId);
        builder.HasIndex(order => order.PlacedAt);

        builder.Property(order => order.CustomerName).HasMaxLength(200);
        builder.Property(order => order.CustomerPhone).HasMaxLength(16);
        builder.Property(order => order.Note).HasMaxLength(500);
        builder.Property(order => order.PaymentMethod).HasConversion<string>().HasMaxLength(30);
        builder.Property(order => order.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(order => order.DeliveryZoneName).HasMaxLength(100);
        builder.Property(order => order.Subtotal).HasPrecision(18, 2);
        builder.Property(order => order.DeliveryFee).HasPrecision(18, 2);
        builder.Property(order => order.VatRatePercent).HasPrecision(5, 2);
        builder.Property(order => order.Vat).HasPrecision(18, 2);
        builder.Property(order => order.Total).HasPrecision(18, 2);
        builder.Property(order => order.AmountPaid).HasPrecision(18, 2);
        builder.Ignore(order => order.PaymentStatus);
        builder.Property(order => order.RowVersion).IsRowVersion();

        builder.ComplexProperty(order => order.DeliveryAddress, address =>
        {
            address.Property(a => a.Line1).HasMaxLength(300);
            address.Property(a => a.Line2).HasMaxLength(300);
            address.Property(a => a.Area).HasMaxLength(100);
            address.Property(a => a.City).HasMaxLength(100);
            address.Property(a => a.PostalCode).HasMaxLength(10);
        });

        builder.HasMany(order => order.Lines).WithOne().HasForeignKey(line => line.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(order => order.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(order => order.History).WithOne().HasForeignKey(change => change.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(order => order.History).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(order => order.Payments).WithOne().HasForeignKey(payment => payment.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(order => order.Payments).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
