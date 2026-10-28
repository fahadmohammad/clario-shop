using ClarioShop.Notifications.Contracts;
using ClarioShop.SharedKernel.Security;

namespace ClarioShop.Notifications.Services;

internal sealed class NotificationsPermissionProvider : IPermissionProvider
{
    public IReadOnlyCollection<PermissionDefinition> GetPermissions() =>
    [
        new(NotificationsPermissions.View, "notifications", "See the SMS and emails sent to shoppers and how each try went"),
    ];
}
