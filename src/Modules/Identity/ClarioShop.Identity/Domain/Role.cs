using ClarioShop.SharedKernel.Domain;

namespace ClarioShop.Identity.Domain;

internal sealed class Role : Entity
{
    private readonly List<RolePermission> permissions = [];

    private Role()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    /// <summary>System roles are seeded by the platform; their permissions are kept in sync automatically.</summary>
    public bool IsSystem { get; private set; }

    public IReadOnlyCollection<RolePermission> Permissions => permissions;

    public static Role Create(string name, bool isSystem) => new()
    {
        Id = Guid.CreateVersion7(),
        Name = name.Trim(),
        NormalizedName = Normalize(name),
        IsSystem = isSystem,
    };

    public static string Normalize(string name) => name.Trim().ToUpperInvariant();

    public void SetPermissions(IEnumerable<string> names)
    {
        var wanted = names.ToHashSet(StringComparer.Ordinal);
        permissions.RemoveAll(permission => !wanted.Contains(permission.Permission));
        foreach (var name in wanted.Where(name => permissions.All(existing => existing.Permission != name)))
        {
            permissions.Add(new RolePermission { RoleId = Id, Permission = name });
        }
    }
}
