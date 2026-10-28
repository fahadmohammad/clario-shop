namespace ClarioShop.Settings.Domain;

/// <summary>Who the store is and how shoppers reach it. Blank optional values are stored as null.</summary>
internal sealed record StoreProfile(
    string StoreName,
    string? ContactPhone,
    string? ContactEmail,
    string? AddressLine1,
    string? AddressLine2,
    string? Area,
    string? City,
    string? PostalCode)
{
    public static StoreProfile Named(string storeName) => new(storeName.Trim(), null, null, null, null, null, null, null);

    public StoreProfile Normalized() => new(
        StoreName.Trim(),
        Optional(ContactPhone),
        Optional(ContactEmail)?.ToLowerInvariant(),
        Optional(AddressLine1),
        Optional(AddressLine2),
        Optional(Area),
        Optional(City),
        Optional(PostalCode));

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
