using ClarioShop.Customers.Domain;

namespace ClarioShop.Customers.Features.Customers;

/// <summary>A customer with addresses, default first, as the staff endpoints return it.</summary>
public sealed record CustomerDetails(
    Guid Id,
    string Phone,
    string? Name,
    string? Email,
    DateTimeOffset CreatedAt,
    IReadOnlyList<AddressItem> Addresses)
{
    internal static CustomerDetails From(Customer customer) => new(
        customer.Id,
        customer.Phone,
        customer.Name,
        customer.Email,
        customer.CreatedAt,
        [.. customer.Addresses.OrderByDescending(a => a.IsDefault).ThenBy(a => a.CreatedAt).Select(AddressItem.From)]);
}

public sealed record AddressItem(
    Guid Id,
    string? Label,
    string RecipientName,
    string RecipientPhone,
    string Line1,
    string? Line2,
    string Area,
    string City,
    string? PostalCode,
    bool IsDefault)
{
    internal static AddressItem From(Address address) => new(
        address.Id,
        address.Label,
        address.RecipientName,
        address.RecipientPhone,
        address.Line1,
        address.Line2,
        address.Area,
        address.City,
        address.PostalCode,
        address.IsDefault);
}
