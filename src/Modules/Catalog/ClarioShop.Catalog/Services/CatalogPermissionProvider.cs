using ClarioShop.Catalog.Contracts;
using ClarioShop.SharedKernel.Security;

namespace ClarioShop.Catalog.Services;

internal sealed class CatalogPermissionProvider : IPermissionProvider
{
    public IReadOnlyCollection<PermissionDefinition> GetPermissions() =>
    [
        new(CatalogPermissions.ProductsView, "catalog", "View all products, including drafts"),
        new(CatalogPermissions.ProductsEdit, "catalog", "Create and edit products"),
        new(CatalogPermissions.ProductsPublish, "catalog", "Publish products to the storefront"),
        new(CatalogPermissions.CategoriesEdit, "catalog", "Create and edit categories"),
    ];
}
