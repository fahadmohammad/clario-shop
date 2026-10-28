using ClarioShop.Settings.Contracts;
using ClarioShop.SharedKernel.Security;

namespace ClarioShop.Settings.Services;

internal sealed class SettingsPermissionProvider : IPermissionProvider
{
    public IReadOnlyCollection<PermissionDefinition> GetPermissions() =>
    [
        new(SettingsPermissions.Edit, "settings", "View and change the store profile, theme, VAT, invoice prefix, languages and delivery zones"),
    ];
}
