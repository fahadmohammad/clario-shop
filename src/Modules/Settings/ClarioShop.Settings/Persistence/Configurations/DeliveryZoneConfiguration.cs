using ClarioShop.Settings.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClarioShop.Settings.Persistence.Configurations;

internal sealed class DeliveryZoneConfiguration : IEntityTypeConfiguration<DeliveryZone>
{
    public void Configure(EntityTypeBuilder<DeliveryZone> builder)
    {
        builder.ToTable("DeliveryZones", table =>
        {
            table.HasCheckConstraint("CK_DeliveryZones_Fee", "\"Fee\" >= 0");
            table.HasCheckConstraint("CK_DeliveryZones_FreeDeliveryThreshold", "\"FreeDeliveryThreshold\" IS NULL OR \"FreeDeliveryThreshold\" > 0");
        });
        builder.HasKey(zone => zone.Id);
        builder.Property(zone => zone.Name).HasMaxLength(100);
        builder.Property(zone => zone.NormalizedName).HasMaxLength(100);
        builder.HasIndex(zone => zone.NormalizedName).IsUnique();
        builder.Property(zone => zone.Areas).HasColumnType("text[]");
        builder.Property(zone => zone.Fee).HasPrecision(18, 2);
        builder.Property(zone => zone.FreeDeliveryThreshold).HasPrecision(18, 2);
        builder.Property(zone => zone.RowVersion).IsRowVersion();
    }
}
