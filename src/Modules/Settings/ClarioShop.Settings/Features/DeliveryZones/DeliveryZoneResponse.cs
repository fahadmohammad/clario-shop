using ClarioShop.Settings.Domain;

namespace ClarioShop.Settings.Features.DeliveryZones;

public sealed record DeliveryZoneResponse(
    Guid Id,
    string Name,
    IReadOnlyList<string> Areas,
    decimal Fee,
    decimal? FreeDeliveryThreshold,
    bool IsActive,
    int SortOrder,
    DateTimeOffset UpdatedAt)
{
    internal static DeliveryZoneResponse From(DeliveryZone zone) =>
        new(zone.Id, zone.Name, [.. zone.Areas], zone.Fee, zone.FreeDeliveryThreshold, zone.IsActive, zone.SortOrder, zone.UpdatedAt);
}
