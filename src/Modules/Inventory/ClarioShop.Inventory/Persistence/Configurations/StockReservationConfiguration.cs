using ClarioShop.Inventory.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClarioShop.Inventory.Persistence.Configurations;

internal sealed class StockReservationConfiguration : IEntityTypeConfiguration<StockReservation>
{
    public void Configure(EntityTypeBuilder<StockReservation> builder)
    {
        builder.ToTable("StockReservations");
        builder.HasKey(reservation => reservation.Id);
        builder.Property(reservation => reservation.ReferenceType).HasMaxLength(50);
        builder.Property(reservation => reservation.Status).HasConversion<string>().HasMaxLength(20);

        // One active reservation per order; also makes concurrent retries of the same checkout collapse into one.
        builder.HasIndex(reservation => new { reservation.ReferenceType, reservation.ReferenceId })
            .IsUnique()
            .HasFilter("\"Status\" = 'Active'")
            .HasDatabaseName("IX_StockReservations_ActiveReference");

        // The expiry sweep looks for active reservations past their time.
        builder.HasIndex(reservation => reservation.ExpiresAt)
            .HasFilter("\"Status\" = 'Active'")
            .HasDatabaseName("IX_StockReservations_ActiveExpiry");

        builder.HasOne<Location>().WithMany().HasForeignKey(reservation => reservation.LocationId).OnDelete(DeleteBehavior.Restrict);

        builder.OwnsMany(reservation => reservation.Lines, line =>
        {
            line.ToTable("StockReservationLines");
            line.WithOwner().HasForeignKey(l => l.ReservationId);
            line.HasKey(l => l.Id);
            line.Property(l => l.Quantity).HasPrecision(18, 3);
            line.HasIndex(l => l.VariantId);
        });
        builder.Navigation(reservation => reservation.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
