namespace ClarioShop.Settings.Contracts;

/// <summary>
/// Read-only, cached access to the current tenant's configuration for other modules (checkout fees and VAT,
/// invoice numbers, notification branding). Changes made through the Settings endpoints are visible immediately.
/// </summary>
public interface ISettingsQueries
{
    /// <summary>The store's settings; a tenant that never saved any gets the defaults, named after the tenant.</summary>
    Task<StoreSettingsInfo> GetStoreSettingsAsync(CancellationToken cancellationToken);

    /// <summary>Active delivery zones in display order.</summary>
    Task<IReadOnlyList<DeliveryZoneInfo>> GetDeliveryZonesAsync(CancellationToken cancellationToken);

    /// <summary>An active delivery zone; null when it does not exist or is inactive.</summary>
    Task<DeliveryZoneInfo?> GetDeliveryZoneAsync(Guid deliveryZoneId, CancellationToken cancellationToken);
}
