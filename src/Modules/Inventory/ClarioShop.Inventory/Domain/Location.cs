using ClarioShop.SharedKernel.Domain;

namespace ClarioShop.Inventory.Domain;

/// <summary>A warehouse or shop that holds stock. Every tenant has exactly one default location.</summary>
internal sealed class Location : Entity
{
    public const string DefaultCode = "MAIN";

    private Location()
    {
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Short unique code staff use on labels and reports, e.g. "MAIN" or "DHK-02".</summary>
    public string Code { get; private set; } = string.Empty;

    public string? Address { get; private set; }

    public bool IsDefault { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Location CreateDefault(DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        Name = "Main store",
        Code = DefaultCode,
        IsDefault = true,
        IsActive = true,
        CreatedAt = now,
    };

    public static Location Create(string name, string code, string? address, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        Name = name.Trim(),
        Code = NormalizeCode(code),
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim(),
        IsDefault = false,
        IsActive = true,
        CreatedAt = now,
    };

    public static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();
}
