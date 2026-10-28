using ClarioShop.Payments.Contracts;
using ClarioShop.SharedKernel.Security;

namespace ClarioShop.Payments.Services;

internal sealed class PaymentsPermissionProvider : IPermissionProvider
{
    public IReadOnlyCollection<PermissionDefinition> GetPermissions() =>
    [
        new(PaymentsPermissions.Manage, "payments", "View an order's payments and record cash collected on delivery"),
    ];
}
