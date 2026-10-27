using ClarioShop.Inventory.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClarioShop.Inventory.Persistence.Configurations;

internal sealed class PurchaseReceiptConfiguration : IEntityTypeConfiguration<PurchaseReceipt>
{
    public void Configure(EntityTypeBuilder<PurchaseReceipt> builder)
    {
        builder.ToTable("PurchaseReceipts");
        builder.HasKey(receipt => receipt.Id);
        builder.Property(receipt => receipt.SupplierName).HasMaxLength(200);
        builder.Property(receipt => receipt.SupplierReference).HasMaxLength(100);
        builder.Property(receipt => receipt.Note).HasMaxLength(1000);
        builder.Ignore(receipt => receipt.TotalCost);
        builder.HasIndex(receipt => receipt.ReceivedAt);
        builder.HasOne<Location>().WithMany().HasForeignKey(receipt => receipt.LocationId).OnDelete(DeleteBehavior.Restrict);

        builder.OwnsMany(receipt => receipt.Lines, line =>
        {
            line.ToTable("PurchaseReceiptLines");
            line.WithOwner().HasForeignKey(l => l.ReceiptId);
            line.HasKey(l => l.Id);
            line.Property(l => l.Quantity).HasPrecision(18, 3);
            line.Property(l => l.UnitCost).HasPrecision(18, 2);
        });
        builder.Navigation(receipt => receipt.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
