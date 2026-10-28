using ClarioShop.Payments.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClarioShop.Payments.Persistence.Configurations;

internal sealed class PaymentAttemptConfiguration : IEntityTypeConfiguration<PaymentAttempt>
{
    public void Configure(EntityTypeBuilder<PaymentAttempt> builder)
    {
        builder.ToTable("PaymentAttempts");
        builder.HasKey(attempt => attempt.Id);
        builder.Property(attempt => attempt.Id).ValueGeneratedNever();
        builder.Property(attempt => attempt.Outcome).HasConversion<string>().HasMaxLength(30);
        builder.Property(attempt => attempt.Note).HasMaxLength(500);
        builder.Property(attempt => attempt.SessionKey).HasMaxLength(100);
        builder.Property(attempt => attempt.ValidationId).HasMaxLength(100);
        builder.Property(attempt => attempt.BankTransactionId).HasMaxLength(100);
        builder.Property(attempt => attempt.CardType).HasMaxLength(100);
    }
}
