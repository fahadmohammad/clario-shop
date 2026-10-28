using ClarioShop.Infrastructure.Tenancy;
using ClarioShop.Settings.Contracts;
using ClarioShop.Settings.Domain;
using ClarioShop.Settings.Persistence;
using ClarioShop.Settings.Services;

namespace ClarioShop.Settings.Features.Store;

/// <summary>Replaces the storefront theme tokens.</summary>
public static class UpdateTheme
{
    public sealed record Command(
        string PrimaryColor,
        string PrimaryForegroundColor,
        string AccentColor,
        string BackgroundColor,
        string ForegroundColor,
        decimal RadiusRem,
        string HeadingFont,
        string BodyFont,
        string? LogoUrl,
        string? FaviconUrl);

    internal sealed class Validator : AbstractValidator<Command>
    {
        private const string HexColor = "^#[0-9A-Fa-f]{6}$";
        private const string ColorMessage = "Use a #RRGGBB colour, e.g. #0F766E.";

        public Validator()
        {
            RuleFor(theme => theme.PrimaryColor).NotEmpty().Matches(HexColor).WithMessage(ColorMessage);
            RuleFor(theme => theme.PrimaryForegroundColor).NotEmpty().Matches(HexColor).WithMessage(ColorMessage);
            RuleFor(theme => theme.AccentColor).NotEmpty().Matches(HexColor).WithMessage(ColorMessage);
            RuleFor(theme => theme.BackgroundColor).NotEmpty().Matches(HexColor).WithMessage(ColorMessage);
            RuleFor(theme => theme.ForegroundColor).NotEmpty().Matches(HexColor).WithMessage(ColorMessage);
            RuleFor(theme => theme.RadiusRem).InclusiveBetween(0m, 4m).PrecisionScale(4, 3, ignoreTrailingZeros: true);

            // Font names end up in CSS; keep them to plain family names.
            RuleFor(theme => theme.HeadingFont).NotEmpty().MaximumLength(100).Matches("^[A-Za-z0-9 -]+$").WithMessage("Use a font family name, e.g. Hind Siliguri.");
            RuleFor(theme => theme.BodyFont).NotEmpty().MaximumLength(100).Matches("^[A-Za-z0-9 -]+$").WithMessage("Use a font family name, e.g. Hind Siliguri.");
            RuleFor(theme => theme.LogoUrl).MaximumLength(2048).Must(BeHttpsUrl).WithMessage("Use an absolute https URL.")
                .When(theme => !string.IsNullOrWhiteSpace(theme.LogoUrl));
            RuleFor(theme => theme.FaviconUrl).MaximumLength(2048).Must(BeHttpsUrl).WithMessage("Use an absolute https URL.")
                .When(theme => !string.IsNullOrWhiteSpace(theme.FaviconUrl));
        }

        private static bool BeHttpsUrl(string? url) =>
            Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
    }

    internal sealed class Handler(SettingsDbContext db, SettingsCache cache, TenantContext tenant, TimeProvider clock) : ICommandHandler<Command, StoreSettingsInfo>
    {
        public async Task<Result<StoreSettingsInfo>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            var settings = await db.LoadOrCreateAsync(tenant, clock, cancellationToken);
            settings.UpdateTheme(
                new Theme(
                    command.PrimaryColor,
                    command.PrimaryForegroundColor,
                    command.AccentColor,
                    command.BackgroundColor,
                    command.ForegroundColor,
                    command.RadiusRem,
                    command.HeadingFont,
                    command.BodyFont,
                    command.LogoUrl,
                    command.FaviconUrl),
                clock.GetUtcNow());
            return await db.SaveStoreSettingsAsync(settings, cache, cancellationToken);
        }
    }
}
