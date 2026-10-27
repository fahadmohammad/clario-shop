using ClarioShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Design;

namespace ClarioShop.Catalog.Persistence;

internal sealed class CatalogDesignTimeFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args) =>
        new(TenantDbContextRegistration.DesignTimeOptions<CatalogDbContext>(CatalogDbContext.SchemaName));
}
