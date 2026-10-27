using ClarioShop.Identity.Persistence;

namespace ClarioShop.Identity.Features.Roles;

public static class ListRoles
{
    public sealed record Query;

    public sealed record RoleItem(Guid Id, string Name, bool IsSystem, IReadOnlyList<string> Permissions);

    internal sealed class Handler(IdentityDbContext db) : ICommandHandler<Query, IReadOnlyList<RoleItem>>
    {
        public async Task<Result<IReadOnlyList<RoleItem>>> HandleAsync(Query query, CancellationToken cancellationToken)
        {
            var roles = await db.Roles.AsNoTracking()
                .OrderBy(role => role.Name)
                .Select(role => new RoleItem(
                    role.Id,
                    role.Name,
                    role.IsSystem,
                    role.Permissions.OrderBy(permission => permission.Permission).Select(permission => permission.Permission).ToList()))
                .ToListAsync(cancellationToken);

            return roles;
        }
    }
}
