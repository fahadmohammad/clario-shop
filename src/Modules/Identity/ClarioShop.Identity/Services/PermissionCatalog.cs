using ClarioShop.SharedKernel.Security;

namespace ClarioShop.Identity.Services;

/// <summary>Every permission declared by every module.</summary>
internal sealed class PermissionCatalog(IEnumerable<IPermissionProvider> providers)
{
    private readonly Lazy<IReadOnlyList<PermissionDefinition>> all = new(() =>
        providers.SelectMany(provider => provider.GetPermissions())
            .DistinctBy(permission => permission.Name)
            .OrderBy(permission => permission.Name, StringComparer.Ordinal)
            .ToList());

    public IReadOnlyList<PermissionDefinition> All => all.Value;

    public bool Exists(string name) => All.Any(permission => permission.Name == name);

    public IEnumerable<string> ForModule(string module) =>
        All.Where(permission => permission.Module == module).Select(permission => permission.Name);
}
