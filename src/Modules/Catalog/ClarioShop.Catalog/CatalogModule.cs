using ClarioShop.Catalog.Contracts;
using ClarioShop.Catalog.Persistence;
using ClarioShop.Catalog.Services;
using ClarioShop.Infrastructure.Handlers;
using ClarioShop.Infrastructure.Persistence;
using ClarioShop.SharedKernel.Security;

namespace ClarioShop.Catalog;

public static class CatalogModule
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services)
    {
        services.AddTenantDbContext<CatalogDbContext>(CatalogDbContext.SchemaName);
        services.AddModuleHandlers(typeof(CatalogModule).Assembly);
        services.AddEndpointGroups(typeof(CatalogModule).Assembly);

        services.AddSingleton<IPermissionProvider, CatalogPermissionProvider>();
        services.AddScoped<SlugAllocator>();
        services.AddScoped<ICatalogQueries, CatalogQueries>();
        return services;
    }
}
