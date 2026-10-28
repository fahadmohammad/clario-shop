using ClarioShop.Settings.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClarioShop.Settings.Persistence.Configurations;

internal sealed class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplate>
{
    public void Configure(EntityTypeBuilder<NotificationTemplate> builder)
    {
        builder.ToTable("NotificationTemplates");
        builder.HasKey(template => template.Id);
        builder.Property(template => template.Event).HasMaxLength(50);
        builder.Property(template => template.Channel).HasMaxLength(20);
        builder.Property(template => template.Language).HasMaxLength(10);
        builder.HasIndex(template => new { template.Event, template.Channel, template.Language }).IsUnique();
        builder.Property(template => template.Subject).HasMaxLength(NotificationTemplate.MaxSubjectLength);
        builder.Property(template => template.Body).HasMaxLength(NotificationTemplate.MaxEmailBodyLength);
        builder.Property(template => template.RowVersion).IsRowVersion();
    }
}
