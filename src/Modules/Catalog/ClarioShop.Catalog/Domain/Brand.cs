using ClarioShop.SharedKernel.Domain;

namespace ClarioShop.Catalog.Domain;

/// <summary>A manufacturer or label products are filed under, e.g. "Pran" or "Square Toiletries".</summary>
internal sealed class Brand : Entity
{
    private Brand()
    {
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Upper-cased name, unique per shop, so "Pran" and "PRAN" cannot become two brands.</summary>
    public string NormalizedName { get; private set; } = string.Empty;

    /// <summary>Set once at creation, so storefront filter links keep working when the brand is renamed.</summary>
    public string Slug { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public static Brand Create(string name, string slug, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        Name = name.Trim(),
        NormalizedName = Normalize(name),
        Slug = slug,
        CreatedAt = now,
    };

    public static string Normalize(string name) => name.Trim().ToUpperInvariant();

    public void Rename(string name)
    {
        Name = name.Trim();
        NormalizedName = Normalize(name);
    }
}
