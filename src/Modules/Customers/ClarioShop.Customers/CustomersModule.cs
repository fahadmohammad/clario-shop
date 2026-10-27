using ClarioShop.Customers.Contracts;
using ClarioShop.Customers.Persistence;
using ClarioShop.Customers.Services;
using ClarioShop.Infrastructure.Handlers;
using ClarioShop.Infrastructure.Persistence;
using ClarioShop.SharedKernel.Security;

namespace ClarioShop.Customers;

public static class CustomersModule
{
    public static IServiceCollection AddCustomersModule(this IServiceCollection services)
    {
        services.AddTenantDbContext<CustomersDbContext>(CustomersDbContext.SchemaName);
        services.AddModuleHandlers(typeof(CustomersModule).Assembly);
        services.AddEndpointGroups(typeof(CustomersModule).Assembly);

        services.AddSingleton<IPermissionProvider, CustomersPermissionProvider>();
        services.AddScoped<ICustomerDirectory, CustomerDirectory>();
        return services;
    }
}
