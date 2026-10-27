using ClarioShop.Identity.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClarioShop.Identity.Persistence.Configurations;

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions");
        builder.HasKey(permission => new { permission.RoleId, permission.Permission });
        builder.Property(permission => permission.Permission).HasMaxLength(100);
    }
}
