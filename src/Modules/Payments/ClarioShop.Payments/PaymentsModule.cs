using ClarioShop.Infrastructure.Handlers;
using ClarioShop.Infrastructure.Persistence;
using ClarioShop.Payments.Gateways;
using ClarioShop.Payments.Gateways.SslCommerz;
using ClarioShop.Payments.Persistence;
using ClarioShop.Payments.Services;
using ClarioShop.SharedKernel.Security;
using Microsoft.Extensions.Configuration;

namespace ClarioShop.Payments;

public static class PaymentsModule
{
    public static IServiceCollection AddPaymentsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddTenantDbContext<PaymentsDbContext>(PaymentsDbContext.SchemaName);
        services.AddModuleHandlers(typeof(PaymentsModule).Assembly);
        services.AddEndpointGroups(typeof(PaymentsModule).Assembly);

        services.AddSingleton<IPermissionProvider, PaymentsPermissionProvider>();

        services.Configure<SslCommerzOptions>(configuration.GetSection(SslCommerzOptions.SectionName));
        services.AddHttpClient<SslCommerzGateway>();
        services.AddScoped<IPaymentGateway>(provider => provider.GetRequiredService<SslCommerzGateway>());
        return services;
    }
}
