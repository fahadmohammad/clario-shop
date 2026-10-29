using ClarioShop.Platform.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClarioShop.Platform.Persistence.Configurations;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");
        builder.HasKey(tenant => tenant.Id);
        builder.Property(tenant => tenant.Identifier).HasMaxLength(40);
        builder.HasIndex(tenant => tenant.Identifier).IsUnique();
        builder.Property(tenant => tenant.Name).HasMaxLength(150);
        builder.Property(tenant => tenant.ConnectionString).HasMaxLength(1000);
        builder.Property(tenant => tenant.Plan).HasConversion<string>().HasMaxLength(20);

        builder.HasMany(tenant => tenant.Hosts).WithOne().HasForeignKey(host => host.TenantId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(tenant => tenant.Hosts).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
