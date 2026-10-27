using ClarioShop.Customers.Contracts;
using ClarioShop.Customers.Domain;

namespace ClarioShop.Customers.Services;

internal static class CustomerMapping
{
    public static CustomerInfo ToInfo(this Customer customer) => new(
        customer.Id,
        customer.Phone,
        customer.Name,
        customer.Email,
        [.. customer.Addresses.OrderByDescending(a => a.IsDefault).ThenBy(a => a.CreatedAt).Select(ToInfo)]);

    public static CustomerAddressInfo ToInfo(this Address address) => new(
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
