using ClarioShop.Inventory.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClarioShop.Inventory.Persistence.Configurations;

internal sealed class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.ToTable("Locations");
        builder.HasKey(location => location.Id);
        builder.Property(location => location.Name).HasMaxLength(100);
        builder.Property(location => location.Code).HasMaxLength(20);
        builder.HasIndex(location => location.Code).IsUnique();
        builder.Property(location => location.Address).HasMaxLength(500);

        // At most one default location per tenant database.
        builder.HasIndex(location => location.IsDefault)
            .IsUnique()
            .HasFilter("\"IsDefault\"")
            .HasDatabaseName("IX_Locations_SingleDefault");
    }
}
