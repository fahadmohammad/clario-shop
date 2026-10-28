using ClarioShop.Reporting.Contracts;
using ClarioShop.SharedKernel.Security;

namespace ClarioShop.Reporting.Services;

internal sealed class ReportingPermissionProvider : IPermissionProvider
{
    public IReadOnlyCollection<PermissionDefinition> GetPermissions() =>
    [
        new(ReportingPermissions.SalesView, "reports", "See sales by day, orders by status and the best-selling variants"),
    ];
}
