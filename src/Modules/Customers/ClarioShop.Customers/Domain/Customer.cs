using ClarioShop.SharedKernel.Domain;
using ClarioShop.SharedKernel.Results;

namespace ClarioShop.Customers.Domain;

/// <summary>
/// A shopper, online or at the till, identified by a normalized mobile number (unique per tenant).
/// Owns its addresses; when it has any, exactly one is the default.
/// </summary>
internal sealed class Customer : Entity
{
    public const int MaxAddresses = 20;

    private readonly List<Address> addresses = [];

    private Customer()
    {
    }

    /// <summary>E.164, e.g. +8801712345678.</summary>
    public string Phone { get; private set; } = string.Empty;

    public string? Name { get; private set; }

    public string? Email { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Concurrency token (PostgreSQL xmin). Every address change touches the customer row.</summary>
    public uint RowVersion { get; private set; }

    public IReadOnlyCollection<Address> Addresses => addresses;

    /// <param name="phone">Already normalized with <see cref="PhoneNumber"/>.</param>
    public static Customer Create(string phone, string? name, string? email, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        Phone = phone,
        Name = Optional(name),
        Email = NormalizeEmail(email),
        CreatedAt = now,
        UpdatedAt = now,
    };

    public void UpdateProfile(string? name, string? email, DateTimeOffset now)
    {
        Name = Optional(name);
        Email = NormalizeEmail(email);
        UpdatedAt = now;
    }

    /// <summary>Sets the name only when the customer has none; returns whether it changed.</summary>
    public bool FillMissingName(string? name, DateTimeOffset now)
    {
        if (Name is not null || Optional(name) is not { } filled)
        {
            return false;
        }

        Name = filled;
        UpdatedAt = now;
        return true;
    }

    /// <summary>Adds an address; the first one, or one added with <paramref name="makeDefault"/>, becomes the default.</summary>
    public Result<Address> AddAddress(AddressDetails details, bool makeDefault, DateTimeOffset now)
    {
        if (addresses.Count >= MaxAddresses)
        {
            return CustomerErrors.TooManyAddresses;
        }

        var address = Address.Create(Id, details, now);
        addresses.Add(address);
        if (makeDefault || addresses.Count == 1)
        {
            MakeDefault(address);
        }

        UpdatedAt = now;
        return address;
    }

    /// <summary>Replaces an address's details. <paramref name="makeDefault"/> false leaves the default unchanged.</summary>
    public Result<Address> UpdateAddress(Guid addressId, AddressDetails details, bool makeDefault, DateTimeOffset now)
    {
        if (addresses.Find(a => a.Id == addressId) is not { } address)
        {
            return CustomerErrors.AddressNotFound;
        }

        address.Apply(details);
        if (makeDefault)
        {
            MakeDefault(address);
        }

        UpdatedAt = now;
        return address;
    }

    /// <summary>Removes an address; when it was the default, the oldest remaining address takes over.</summary>
    public Result RemoveAddress(Guid addressId, DateTimeOffset now)
    {
        if (addresses.Find(a => a.Id == addressId) is not { } address)
        {
            return CustomerErrors.AddressNotFound;
        }

        addresses.Remove(address);
        if (address.IsDefault && addresses.Count > 0)
        {
            MakeDefault(addresses.MinBy(a => (a.CreatedAt, a.Id))!);
        }

        UpdatedAt = now;
        return Result.Success();
    }

    private void MakeDefault(Address chosen)
    {
        foreach (var address in addresses)
        {
            address.SetDefault(address == chosen);
        }
    }

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeEmail(string? email) => Optional(email)?.ToLowerInvariant();
}
