using ClarioShop.Inventory.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClarioShop.Inventory.Persistence.Configurations;

internal sealed class StockTransferConfiguration : IEntityTypeConfiguration<StockTransfer>
{
    public void Configure(EntityTypeBuilder<StockTransfer> builder)
    {
        builder.ToTable("StockTransfers", table => table.HasCheckConstraint(
            "CK_StockTransfers_DifferentLocations", "\"FromLocationId\" <> \"ToLocationId\""));
        builder.HasKey(transfer => transfer.Id);
        builder.Property(transfer => transfer.Note).HasMaxLength(1000);
        builder.HasIndex(transfer => transfer.RecordedAt);
        builder.HasOne<Location>().WithMany().HasForeignKey(transfer => transfer.FromLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Location>().WithMany().HasForeignKey(transfer => transfer.ToLocationId).OnDelete(DeleteBehavior.Restrict);

        builder.OwnsMany(transfer => transfer.Lines, line =>
        {
            line.ToTable("StockTransferLines", table => table.HasCheckConstraint("CK_StockTransferLines_Quantity", "\"Quantity\" > 0"));
            line.WithOwner().HasForeignKey(l => l.TransferId);
            line.HasKey(l => l.Id);
            line.Property(l => l.Quantity).HasPrecision(18, 3);
            line.HasIndex(l => l.VariantId);
        });
        builder.Navigation(transfer => transfer.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
