using ClarioShop.Payments.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClarioShop.Payments.Persistence.Configurations;

internal sealed class GatewayWebhookLogConfiguration : IEntityTypeConfiguration<GatewayWebhookLog>
{
    public void Configure(EntityTypeBuilder<GatewayWebhookLog> builder)
    {
        builder.ToTable("GatewayWebhookLogs");
        builder.HasKey(log => log.Id);
        builder.Property(log => log.Id).ValueGeneratedNever();
        builder.HasIndex(log => log.TransactionId);
        builder.HasIndex(log => log.ReceivedAt);
        builder.Property(log => log.Gateway).HasMaxLength(30);
        builder.Property(log => log.Source).HasConversion<string>().HasMaxLength(30);
        builder.Property(log => log.Payload).HasColumnType("jsonb");
        builder.Property(log => log.TransactionId).HasMaxLength(GatewayWebhookLog.MaxIdLength);
        builder.Property(log => log.ValidationId).HasMaxLength(GatewayWebhookLog.MaxIdLength);
        builder.Property(log => log.ReportedStatus).HasMaxLength(GatewayWebhookLog.MaxIdLength);
        builder.Property(log => log.Outcome).HasConversion<string>().HasMaxLength(30);
        builder.Property(log => log.Detail).HasMaxLength(GatewayWebhookLog.MaxDetailLength);
    }
}
