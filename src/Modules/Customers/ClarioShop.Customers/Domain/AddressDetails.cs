namespace ClarioShop.Customers.Domain;

/// <param name="RecipientPhone">Already normalized with <see cref="PhoneNumber"/>.</param>
internal sealed record AddressDetails(
    string? Label,
    string RecipientName,
    string RecipientPhone,
    string Line1,
    string? Line2,
    string Area,
    string City,
    string? PostalCode);
