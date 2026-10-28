using ClarioShop.Infrastructure.Handlers;
using ClarioShop.Infrastructure.Persistence;
using ClarioShop.Payments.Persistence;
using ClarioShop.Payments.Services;
using ClarioShop.SharedKernel.Security;

namespace ClarioShop.Payments;

public static class PaymentsModule
{
    public static IServiceCollection AddPaymentsModule(this IServiceCollection services)
    {
        services.AddTenantDbContext<PaymentsDbContext>(PaymentsDbContext.SchemaName);
        services.AddModuleHandlers(typeof(PaymentsModule).Assembly);
        services.AddEndpointGroups(typeof(PaymentsModule).Assembly);

        services.AddSingleton<IPermissionProvider, PaymentsPermissionProvider>();
        return services;
    }
}
