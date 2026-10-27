using ClarioShop.Identity.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClarioShop.Identity.Persistence.Configurations;

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");
        builder.HasKey(role => role.Id);
        builder.Property(role => role.Name).HasMaxLength(100);
        builder.Property(role => role.NormalizedName).HasMaxLength(100);
        builder.HasIndex(role => role.NormalizedName).IsUnique();

        builder.HasMany(role => role.Permissions).WithOne().HasForeignKey(permission => permission.RoleId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(role => role.Permissions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
