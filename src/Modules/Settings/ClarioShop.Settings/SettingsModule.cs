using ClarioShop.Infrastructure.Handlers;
using ClarioShop.Infrastructure.Persistence;
using ClarioShop.Settings.Contracts;
using ClarioShop.Settings.Persistence;
using ClarioShop.Settings.Services;
using ClarioShop.SharedKernel.Security;

namespace ClarioShop.Settings;

public static class SettingsModule
{
    public static IServiceCollection AddSettingsModule(this IServiceCollection services)
    {
        services.AddTenantDbContext<SettingsDbContext>(SettingsDbContext.SchemaName);
        services.AddModuleHandlers(typeof(SettingsModule).Assembly);
        services.AddEndpointGroups(typeof(SettingsModule).Assembly);

        services.AddSingleton<IPermissionProvider, SettingsPermissionProvider>();
        services.AddScoped<SettingsCache>();
        services.AddScoped<ISettingsQueries, SettingsQueries>();
        services.AddScoped<GatewaySecretProtector>();
        services.AddScoped<IPaymentGatewayCredentials, PaymentGatewayCredentials>();
        return services;
    }
}
