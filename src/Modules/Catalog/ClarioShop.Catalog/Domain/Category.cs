using ClarioShop.SharedKernel.Domain;

namespace ClarioShop.Catalog.Domain;

internal sealed class Category : Entity
{
    private Category()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public Guid? ParentId { get; private set; }

    public int SortOrder { get; private set; }

    public static Category Create(string name, string slug, Guid? parentId, int sortOrder) => new()
    {
        Id = Guid.CreateVersion7(),
        Name = name.Trim(),
        Slug = slug,
        ParentId = parentId,
        SortOrder = sortOrder,
    };
}
