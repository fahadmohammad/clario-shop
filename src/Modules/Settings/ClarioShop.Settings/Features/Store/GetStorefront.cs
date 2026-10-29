using ClarioShop.Settings.Contracts;
using ClarioShop.SharedKernel.Tenancy;

namespace ClarioShop.Settings.Features.Store;

/// <summary>
/// What the storefront and apps need before rendering: store name and contacts, theme tokens, currency format,
/// languages, the delivery zones shoppers can pick and the features the shop's plan includes (so the apps hide the
/// rest). Public and cached; holds nothing private.
/// </summary>
public static class GetStorefront
{
    public sealed record Query;

    public sealed record Response(
        string StoreName,
        string? ContactPhone,
        string? ContactEmail,
        ThemeInfo Theme,
        CurrencyFormatInfo Currency,
        string DefaultLanguage,
        IReadOnlyList<string> Languages,
        IReadOnlyList<DeliveryZoneInfo> DeliveryZones,
        IReadOnlyList<PlanFeatures> Features);

    internal sealed class Handler(ISettingsQueries settings, ITenantContext tenant) : ICommandHandler<Query, Response>
    {
        public async Task<Result<Response>> HandleAsync(Query query, CancellationToken cancellationToken)
        {
            var store = await settings.GetStoreSettingsAsync(cancellationToken);
            var zones = await settings.GetDeliveryZonesAsync(cancellationToken);
            return new Response(
                store.Profile.StoreName,
                store.Profile.ContactPhone,
                store.Profile.ContactEmail,
                store.Theme,
                store.Currency,
                store.DefaultLanguage,
                store.Languages,
                zones,
                [.. Enum.GetValues<PlanFeatures>().Where(feature => feature != PlanFeatures.None && tenant.Features.HasFlag(feature))]);
        }
    }
}
