using ClarioShop.Identity.Contracts;
using ClarioShop.SharedKernel.Security;

namespace ClarioShop.Identity.Services;

internal sealed class IdentityPermissionProvider : IPermissionProvider
{
    public IReadOnlyCollection<PermissionDefinition> GetPermissions() =>
    [
        new(IdentityPermissions.UsersView, "identity", "View staff accounts"),
        new(IdentityPermissions.UsersManage, "identity", "Create and change staff accounts"),
        new(IdentityPermissions.RolesView, "identity", "View roles and their permissions"),
    ];
}
