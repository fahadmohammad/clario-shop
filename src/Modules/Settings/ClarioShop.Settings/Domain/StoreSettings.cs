using ClarioShop.SharedKernel.Domain;
using ClarioShop.SharedKernel.Results;

namespace ClarioShop.Settings.Domain;

/// <summary>
/// The tenant's store configuration: one row per tenant database, under a fixed id so that two first saves
/// cannot create two rows. Until it is first saved, readers use <see cref="CreateDefault"/>.
/// </summary>
internal sealed class StoreSettings : Entity
{
    public const string DefaultInvoicePrefix = "INV";

    public static readonly Guid SingletonId = new("01000000-0000-7000-8000-000000000001");

    public static readonly IReadOnlyList<string> SupportedLanguages = ["bn", "en"];

    private StoreSettings()
    {
    }

    public StoreProfile Profile { get; private set; } = null!;

    public Theme Theme { get; private set; } = null!;

    public string InvoicePrefix { get; private set; } = DefaultInvoicePrefix;

    /// <summary>VAT in percent, e.g. 15 for 15%.</summary>
    public decimal VatRatePercent { get; private set; }

    public CurrencyFormat Currency { get; private set; } = null!;

    public string DefaultLanguage { get; private set; } = "en";

    /// <summary>Offered languages, default first.</summary>
    public List<string> Languages { get; private set; } = [];

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Concurrency token (PostgreSQL xmin).</summary>
    public uint RowVersion { get; private set; }

    public static StoreSettings CreateDefault(string storeName, DateTimeOffset now) => new()
    {
        Id = SingletonId,
        Profile = StoreProfile.Named(storeName),
        Theme = Theme.Default,
        InvoicePrefix = DefaultInvoicePrefix,
        VatRatePercent = 0m,
        Currency = CurrencyFormat.Default,
        DefaultLanguage = "en",
        Languages = ["en", "bn"],
        CreatedAt = now,
        UpdatedAt = now,
    };

    /// <summary>
    /// Replaces the profile and commerce settings. Languages are lower-cased and de-duplicated, with the default
    /// moved first; fails when the default is not among them.
    /// </summary>
    public Result Update(
        StoreProfile profile,
        string invoicePrefix,
        decimal vatRatePercent,
        CurrencyFormat currency,
        string defaultLanguage,
        IEnumerable<string> offeredLanguages,
        DateTimeOffset now)
    {
        var normalizedDefault = defaultLanguage.Trim().ToLowerInvariant();
        var normalizedLanguages = offeredLanguages.Select(language => language.Trim().ToLowerInvariant()).Distinct().ToList();
        if (!normalizedLanguages.Remove(normalizedDefault))
        {
            return StoreSettingsErrors.DefaultLanguageNotOffered;
        }

        Profile = profile.Normalized();
        InvoicePrefix = invoicePrefix.Trim().ToUpperInvariant();
        VatRatePercent = vatRatePercent;
        Currency = currency.Normalized();
        DefaultLanguage = normalizedDefault;
        Languages = [normalizedDefault, .. normalizedLanguages];
        UpdatedAt = now;
        return Result.Success();
    }

    public void UpdateTheme(Theme theme, DateTimeOffset now)
    {
        Theme = theme.Normalized();
        UpdatedAt = now;
    }
}
