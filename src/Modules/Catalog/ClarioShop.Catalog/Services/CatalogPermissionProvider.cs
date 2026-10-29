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
        new(CatalogPermissions.BrandsEdit, "catalog", "Create and rename brands"),
        new(CatalogPermissions.AttributesEdit, "catalog", "Create and edit variant attributes and their values"),

        // Kept out of the "catalog" group so only the Owner gets it, not the Catalog Manager whose changes it shows.
        new(CatalogPermissions.AuditView, "audit", "View the audit log of price changes"),
    ];
}
