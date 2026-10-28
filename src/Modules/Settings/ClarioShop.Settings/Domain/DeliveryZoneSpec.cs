namespace ClarioShop.Settings.Domain;

internal sealed record DeliveryZoneSpec(
    string Name,
    IReadOnlyList<string> Areas,
    decimal Fee,
    decimal? FreeDeliveryThreshold,
    bool IsActive,
    int SortOrder);
