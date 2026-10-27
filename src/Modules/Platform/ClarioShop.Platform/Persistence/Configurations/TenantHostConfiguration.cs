using ClarioShop.Platform.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClarioShop.Platform.Persistence.Configurations;

internal sealed class TenantHostConfiguration : IEntityTypeConfiguration<TenantHost>
{
    public void Configure(EntityTypeBuilder<TenantHost> builder)
    {
        builder.ToTable("TenantHosts");
        builder.HasKey(host => host.Host);
        builder.Property(host => host.Host).HasMaxLength(253);
    }
}
