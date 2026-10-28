using ClarioShop.Infrastructure.Tenancy;
using ClarioShop.Settings.Contracts;
using ClarioShop.Settings.Domain;
using ClarioShop.Settings.Persistence;
using ClarioShop.Settings.Services;

namespace ClarioShop.Settings.Features.Store;

/// <summary>Replaces the store profile, invoice prefix, VAT rate, currency format and languages.</summary>
public static class UpdateStoreSettings
{
    public sealed record Command(
        ProfileInput Profile,
        string InvoicePrefix,
        decimal VatRatePercent,
        CurrencyInput Currency,
        string DefaultLanguage,
        IReadOnlyList<string> Languages);

    public sealed record ProfileInput(
        string StoreName,
        string? ContactPhone,
        string? ContactEmail,
        string? AddressLine1,
        string? AddressLine2,
        string? Area,
        string? City,
        string? PostalCode);

    public sealed record CurrencyInput(string Symbol, CurrencySymbolPosition SymbolPosition, int DecimalDigits, DigitGrouping Grouping);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.Profile).NotNull().SetValidator(new ProfileValidator());
            RuleFor(command => command.InvoicePrefix).NotEmpty().MaximumLength(10)
                .Matches("^[A-Za-z0-9-]+$").WithMessage("Use letters, digits and dashes only.");
            RuleFor(command => command.VatRatePercent).InclusiveBetween(0m, 100m).PrecisionScale(5, 2, ignoreTrailingZeros: true);
            RuleFor(command => command.Currency).NotNull().SetValidator(new CurrencyValidator());
            RuleFor(command => command.Languages).NotEmpty();
            RuleForEach(command => command.Languages)
                .Must(language => StoreSettings.SupportedLanguages.Contains(language?.Trim().ToLowerInvariant()))
                .WithMessage($"Supported languages are {string.Join(", ", StoreSettings.SupportedLanguages)}.");
            RuleFor(command => command.DefaultLanguage).NotEmpty()
                .Must((command, language) => command.Languages?.Any(l => string.Equals(l?.Trim(), language?.Trim(), StringComparison.OrdinalIgnoreCase)) == true)
                .WithMessage("The default language must be one of the store's languages.");
        }
    }

    internal sealed class ProfileValidator : AbstractValidator<ProfileInput>
    {
        public ProfileValidator()
        {
            RuleFor(profile => profile.StoreName).NotEmpty().MaximumLength(200);
            RuleFor(profile => profile.ContactPhone).MaximumLength(20)
                .Matches(@"^\+?[0-9][0-9 -]*$").WithMessage("Enter a phone number using digits, spaces, dashes and an optional leading +.")
                .When(profile => !string.IsNullOrWhiteSpace(profile.ContactPhone));
            RuleFor(profile => profile.ContactEmail).MaximumLength(254).EmailAddress()
                .When(profile => !string.IsNullOrWhiteSpace(profile.ContactEmail));
            RuleFor(profile => profile.AddressLine1).MaximumLength(200);
            RuleFor(profile => profile.AddressLine2).MaximumLength(200);
            RuleFor(profile => profile.Area).MaximumLength(100);
            RuleFor(profile => profile.City).MaximumLength(100);
            RuleFor(profile => profile.PostalCode).MaximumLength(10);
        }
    }

    internal sealed class CurrencyValidator : AbstractValidator<CurrencyInput>
    {
        public CurrencyValidator()
        {
            RuleFor(currency => currency.Symbol).NotEmpty().MaximumLength(5);
            RuleFor(currency => currency.SymbolPosition).IsInEnum();
            RuleFor(currency => currency.DecimalDigits).InclusiveBetween(0, 2);
            RuleFor(currency => currency.Grouping).IsInEnum();
        }
    }

    internal sealed class Handler(SettingsDbContext db, SettingsCache cache, TenantContext tenant, TimeProvider clock) : ICommandHandler<Command, StoreSettingsInfo>
    {
        public async Task<Result<StoreSettingsInfo>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            var settings = await db.LoadOrCreateAsync(tenant, clock, cancellationToken);
            var profile = command.Profile;
            var currency = command.Currency;
            var updated = settings.Update(
                new StoreProfile(profile.StoreName, profile.ContactPhone, profile.ContactEmail, profile.AddressLine1, profile.AddressLine2, profile.Area, profile.City, profile.PostalCode),
                command.InvoicePrefix,
                command.VatRatePercent,
                new CurrencyFormat(currency.Symbol, currency.SymbolPosition, currency.DecimalDigits, currency.Grouping),
                command.DefaultLanguage,
                command.Languages,
                clock.GetUtcNow());
            if (updated.IsFailure)
            {
                return updated.Error;
            }

            return await db.SaveStoreSettingsAsync(settings, cache, cancellationToken);
        }
    }
}
