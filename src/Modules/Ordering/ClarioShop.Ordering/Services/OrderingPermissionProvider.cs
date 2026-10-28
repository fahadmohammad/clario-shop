using ClarioShop.Ordering.Contracts;
using ClarioShop.SharedKernel.Security;

namespace ClarioShop.Ordering.Services;

internal sealed class OrderingPermissionProvider : IPermissionProvider
{
    public IReadOnlyCollection<PermissionDefinition> GetPermissions() =>
    [
        new(OrderingPermissions.OrdersView, "orders", "View orders, their lines and status history"),
    ];
}
