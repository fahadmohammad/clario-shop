using ClarioShop.Settings.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClarioShop.Settings.Persistence.Configurations;

internal sealed class PaymentGatewayAccountConfiguration : IEntityTypeConfiguration<PaymentGatewayAccount>
{
    public void Configure(EntityTypeBuilder<PaymentGatewayAccount> builder)
    {
        builder.ToTable("PaymentGatewayAccounts");
        builder.HasKey(account => account.Id);
        builder.Property(account => account.Gateway).HasConversion<string>().HasMaxLength(30);
        builder.HasIndex(account => account.Gateway).IsUnique();
        builder.Property(account => account.MerchantId).HasMaxLength(100);
        builder.Property(account => account.ProtectedSecret).HasMaxLength(2000);
        builder.Property(account => account.RowVersion).IsRowVersion();
    }
}
