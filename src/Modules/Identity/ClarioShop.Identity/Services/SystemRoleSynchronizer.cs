using ClarioShop.Identity.Contracts;
using ClarioShop.Identity.Domain;
using ClarioShop.Identity.Persistence;
using ClarioShop.Infrastructure.Tenancy;

namespace ClarioShop.Identity.Services;

/// <summary>Creates the system roles and keeps their permissions current as modules add permissions.</summary>
internal sealed class SystemRoleSynchronizer(IdentityDbContext db, PermissionCatalog catalog) : ITenantInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var definitions = new Dictionary<string, IEnumerable<string>>
        {
            [SystemRoles.Owner] = catalog.All.Select(permission => permission.Name),
            [SystemRoles.CatalogManager] = catalog.ForModule("catalog"),
        };

        var existing = await db.Roles.Include(role => role.Permissions)
            .Where(role => role.IsSystem)
            .ToListAsync(cancellationToken);

        foreach (var (name, permissions) in definitions)
        {
            var role = existing.FirstOrDefault(r => r.NormalizedName == Role.Normalize(name));
            if (role is null)
            {
                role = Role.Create(name, isSystem: true);
                db.Roles.Add(role);
            }

            role.SetPermissions(permissions);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
