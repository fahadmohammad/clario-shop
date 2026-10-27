using ClarioShop.Customers.Contracts;
using ClarioShop.SharedKernel.Security;

namespace ClarioShop.Customers.Services;

internal sealed class CustomersPermissionProvider : IPermissionProvider
{
    public IReadOnlyCollection<PermissionDefinition> GetPermissions() =>
    [
        new(CustomersPermissions.View, "customers", "Search customers and view their profiles and addresses"),
        new(CustomersPermissions.Edit, "customers", "Create and edit customers and their addresses"),
    ];
}
