using ClarioShop.Identity.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClarioShop.Identity.Persistence.Configurations;

internal sealed class OtpCodeConfiguration : IEntityTypeConfiguration<OtpCode>
{
    public void Configure(EntityTypeBuilder<OtpCode> builder)
    {
        builder.ToTable("OtpCodes");
        builder.HasKey(otp => otp.Id);
        builder.Property(otp => otp.Phone).HasMaxLength(16);
        builder.Property(otp => otp.CodeHash).HasMaxLength(32);
        builder.HasIndex(otp => new { otp.Phone, otp.CreatedAt });
    }
}
