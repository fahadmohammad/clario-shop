using ClarioShop.Identity.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClarioShop.Identity.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Email).HasMaxLength(256);
        builder.Property(user => user.NormalizedEmail).HasMaxLength(256);
        builder.HasIndex(user => user.NormalizedEmail).IsUnique();
        builder.Property(user => user.FullName).HasMaxLength(200);
        builder.Property(user => user.PasswordHash).HasMaxLength(500);

        builder.HasMany(user => user.Roles).WithOne().HasForeignKey(role => role.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(user => user.Roles).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
