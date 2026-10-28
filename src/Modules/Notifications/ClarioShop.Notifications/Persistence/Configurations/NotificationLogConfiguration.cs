using ClarioShop.Notifications.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClarioShop.Notifications.Persistence.Configurations;

internal sealed class NotificationLogConfiguration : IEntityTypeConfiguration<NotificationLog>
{
    public void Configure(EntityTypeBuilder<NotificationLog> builder)
    {
        builder.ToTable("NotificationLogs");
        builder.HasKey(log => log.Id);
        builder.Property(log => log.Id).ValueGeneratedNever();

        // One message per event and channel: a redelivered event finds it instead of sending again.
        builder.HasIndex(log => new { log.EventId, log.Channel }).IsUnique();
        builder.HasIndex(log => log.OrderId);
        builder.HasIndex(log => log.NextAttemptAt).HasFilter("\"Status\" = 'Pending'");

        builder.Property(log => log.Kind).HasConversion<string>().HasMaxLength(30);
        builder.Property(log => log.OrderNumber).HasMaxLength(30);
        builder.Property(log => log.Channel).HasConversion<string>().HasMaxLength(10);
        builder.Property(log => log.Recipient).HasMaxLength(NotificationLog.MaxRecipientLength);
        builder.Property(log => log.Language).HasMaxLength(10);
        builder.Property(log => log.Subject).HasMaxLength(NotificationLog.MaxSubjectLength);
        builder.Property(log => log.Body).HasMaxLength(NotificationLog.MaxBodyLength);
        builder.Property(log => log.Status).HasConversion<string>().HasMaxLength(10);
        builder.Property(log => log.LastError).HasMaxLength(NotificationLog.MaxErrorLength);
        builder.Property(log => log.RowVersion).IsRowVersion();
    }
}
