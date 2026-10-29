using ClarioShop.SharedKernel.Tenancy;
using Microsoft.Extensions.Configuration;

namespace ClarioShop.Infrastructure.Tenancy;

public static class TenancyRegistration
{
    public static IServiceCollection AddClarioTenancy(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TenancyOptions>(configuration.GetSection(TenancyOptions.SectionName));
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<TenantContext>());
        services.AddSingleton<TenantScopeFactory>();
        services.AddSingleton<TenantMigrator>();
        return services;
    }

    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app) =>
        app.UseMiddleware<TenantResolutionMiddleware>();

    /// <summary>Enforces <see cref="PlanFeatureEndpointExtensions.RequirePlanFeature"/>. Goes after authorization.</summary>
    public static IApplicationBuilder UsePlanGate(this IApplicationBuilder app) =>
        app.UseMiddleware<PlanGateMiddleware>();
}
