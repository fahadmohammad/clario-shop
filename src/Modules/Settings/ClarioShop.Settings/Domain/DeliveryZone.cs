using ClarioShop.SharedKernel.Domain;

namespace ClarioShop.Settings.Domain;

/// <summary>
/// A delivery area with its fee, e.g. "Inside Dhaka" at 60 BDT, free from 2,000 BDT. Names are unique per tenant
/// ignoring case. Inactive zones are kept for staff but not offered to shoppers.
/// </summary>
internal sealed class DeliveryZone : Entity
{
    private DeliveryZone()
    {
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Upper-case name, unique: "inside dhaka" and "Inside Dhaka" are the same zone.</summary>
    public string NormalizedName { get; private set; } = string.Empty;

    /// <summary>Covered areas, trimmed, without duplicates (ignoring case), in the order given.</summary>
    public List<string> Areas { get; private set; } = [];

    public decimal Fee { get; private set; }

    /// <summary>Order subtotal from which delivery is free; null when it never is.</summary>
    public decimal? FreeDeliveryThreshold { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>Display order for shoppers; ties sort by name.</summary>
    public int SortOrder { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Concurrency token (PostgreSQL xmin).</summary>
    public uint RowVersion { get; private set; }

    public static DeliveryZone Create(DeliveryZoneSpec spec, DateTimeOffset now)
    {
        var zone = new DeliveryZone { Id = Guid.CreateVersion7(), CreatedAt = now };
        zone.Apply(spec, now);
        return zone;
    }

    public static string Normalize(string name) => name.Trim().ToUpperInvariant();

    public void Apply(DeliveryZoneSpec spec, DateTimeOffset now)
    {
        Name = spec.Name.Trim();
        NormalizedName = Normalize(spec.Name);
        Areas = spec.Areas
            .Where(area => !string.IsNullOrWhiteSpace(area))
            .Select(area => area.Trim())
            .DistinctBy(area => area.ToUpperInvariant())
            .ToList();
        Fee = spec.Fee;
        FreeDeliveryThreshold = spec.FreeDeliveryThreshold;
        IsActive = spec.IsActive;
        SortOrder = spec.SortOrder;
        UpdatedAt = now;
    }
}
