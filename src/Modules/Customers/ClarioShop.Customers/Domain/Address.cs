using ClarioShop.SharedKernel.Domain;

namespace ClarioShop.Customers.Domain;

/// <summary>A delivery address. Belongs to one customer; changed only through <see cref="Customer"/>.</summary>
internal sealed class Address : Entity
{
    private Address()
    {
    }

    public Guid CustomerId { get; private set; }

    /// <summary>What the customer calls it, e.g. "Home" or "Office".</summary>
    public string? Label { get; private set; }

    public string RecipientName { get; private set; } = string.Empty;

    /// <summary>E.164; the rider calls this number.</summary>
    public string RecipientPhone { get; private set; } = string.Empty;

    public string Line1 { get; private set; } = string.Empty;

    public string? Line2 { get; private set; }

    /// <summary>Thana or neighbourhood, e.g. "Dhanmondi"; delivery zones match on it.</summary>
    public string Area { get; private set; } = string.Empty;

    /// <summary>City or district, e.g. "Dhaka".</summary>
    public string City { get; private set; } = string.Empty;

    public string? PostalCode { get; private set; }

    public bool IsDefault { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    internal static Address Create(Guid customerId, AddressDetails details, DateTimeOffset now)
    {
        var address = new Address { Id = Guid.CreateVersion7(), CustomerId = customerId, CreatedAt = now };
        address.Apply(details);
        return address;
    }

    internal void Apply(AddressDetails details)
    {
        Label = Optional(details.Label);
        RecipientName = details.RecipientName.Trim();
        RecipientPhone = details.RecipientPhone;
        Line1 = details.Line1.Trim();
        Line2 = Optional(details.Line2);
        Area = details.Area.Trim();
        City = details.City.Trim();
        PostalCode = Optional(details.PostalCode);
    }

    internal void SetDefault(bool isDefault) => IsDefault = isDefault;

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
