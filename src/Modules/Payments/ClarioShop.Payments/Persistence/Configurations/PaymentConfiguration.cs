using ClarioShop.Payments.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClarioShop.Payments.Persistence.Configurations;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments", table => table.HasCheckConstraint("CK_Payments_Amount", "\"Amount\" > 0"));
        builder.HasKey(payment => payment.Id);
        builder.HasIndex(payment => payment.OrderId);
        builder.Property(payment => payment.OrderNumber).HasMaxLength(30);
        builder.Property(payment => payment.Method).HasConversion<string>().HasMaxLength(30);
        builder.Property(payment => payment.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(payment => payment.Amount).HasPrecision(18, 2);
        builder.Property(payment => payment.RowVersion).IsRowVersion();

        builder.HasMany(payment => payment.Attempts).WithOne().HasForeignKey(attempt => attempt.PaymentId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(payment => payment.Attempts).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
