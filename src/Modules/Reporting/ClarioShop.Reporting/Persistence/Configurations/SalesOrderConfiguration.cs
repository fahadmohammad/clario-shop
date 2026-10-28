using ClarioShop.Reporting.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClarioShop.Reporting.Persistence.Configurations;

internal sealed class SalesOrderConfiguration : IEntityTypeConfiguration<SalesOrder>
{
    public void Configure(EntityTypeBuilder<SalesOrder> builder)
    {
        builder.ToTable("SalesOrders");
        builder.HasKey(order => order.Id);
        builder.Property(order => order.Id).ValueGeneratedNever();

        builder.Property(order => order.Number).HasMaxLength(30);
        builder.Property(order => order.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(order => order.PaymentMethod).HasMaxLength(30);
        builder.Property(order => order.Subtotal).HasPrecision(18, 2);
        builder.Property(order => order.DeliveryFee).HasPrecision(18, 2);
        builder.Property(order => order.Vat).HasPrecision(18, 2);
        builder.Property(order => order.Total).HasPrecision(18, 2);
        builder.Property(order => order.RowVersion).IsRowVersion();
        builder.Ignore(order => order.IsSale);

        // The daily sales and top variants reports read by sale day, orders by status by the day placed.
        builder.HasIndex(order => order.SoldOn);
        builder.HasIndex(order => order.PlacedOn);

        builder.HasMany(order => order.Lines)
            .WithOne()
            .HasForeignKey(line => line.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(order => order.Lines).HasField("lines").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
