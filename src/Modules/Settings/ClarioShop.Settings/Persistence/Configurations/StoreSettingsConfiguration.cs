using ClarioShop.Settings.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClarioShop.Settings.Persistence.Configurations;

internal sealed class StoreSettingsConfiguration : IEntityTypeConfiguration<StoreSettings>
{
    public void Configure(EntityTypeBuilder<StoreSettings> builder)
    {
        builder.ToTable("StoreSettings", table => table.HasCheckConstraint(
            "CK_StoreSettings_Singleton", $"\"Id\" = '{StoreSettings.SingletonId}'"));
        builder.HasKey(settings => settings.Id);
        builder.Property(settings => settings.Id).ValueGeneratedNever();

        builder.ComplexProperty(settings => settings.Profile, profile =>
        {
            profile.Property(p => p.StoreName).HasMaxLength(200);
            profile.Property(p => p.ContactPhone).HasMaxLength(20);
            profile.Property(p => p.ContactEmail).HasMaxLength(254);
            profile.Property(p => p.AddressLine1).HasMaxLength(200);
            profile.Property(p => p.AddressLine2).HasMaxLength(200);
            profile.Property(p => p.Area).HasMaxLength(100);
            profile.Property(p => p.City).HasMaxLength(100);
            profile.Property(p => p.PostalCode).HasMaxLength(10);
        });

        builder.ComplexProperty(settings => settings.Theme, theme =>
        {
            theme.Property(t => t.PrimaryColor).HasMaxLength(7);
            theme.Property(t => t.PrimaryForegroundColor).HasMaxLength(7);
            theme.Property(t => t.AccentColor).HasMaxLength(7);
            theme.Property(t => t.BackgroundColor).HasMaxLength(7);
            theme.Property(t => t.ForegroundColor).HasMaxLength(7);
            theme.Property(t => t.RadiusRem).HasPrecision(4, 3);
            theme.Property(t => t.HeadingFont).HasMaxLength(100);
            theme.Property(t => t.BodyFont).HasMaxLength(100);
            theme.Property(t => t.LogoUrl).HasMaxLength(2048);
            theme.Property(t => t.FaviconUrl).HasMaxLength(2048);
        });

        builder.ComplexProperty(settings => settings.Currency, currency =>
        {
            currency.Property(c => c.Symbol).HasMaxLength(5);
            currency.Property(c => c.SymbolPosition).HasConversion<string>().HasMaxLength(10);
            currency.Property(c => c.Grouping).HasConversion<string>().HasMaxLength(20);
        });

        builder.Property(settings => settings.InvoicePrefix).HasMaxLength(10);
        builder.Property(settings => settings.VatRatePercent).HasPrecision(5, 2);
        builder.Property(settings => settings.DefaultLanguage).HasMaxLength(2);
        builder.Property(settings => settings.Languages).HasColumnType("text[]");
        builder.Property(settings => settings.RowVersion).IsRowVersion();
    }
}
