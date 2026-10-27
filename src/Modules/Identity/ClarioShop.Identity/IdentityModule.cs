using ClarioShop.Identity.Contracts;
using ClarioShop.Identity.Domain;
using ClarioShop.Identity.Persistence;
using ClarioShop.Identity.Services;
using ClarioShop.Infrastructure.Handlers;
using ClarioShop.Infrastructure.Persistence;
using ClarioShop.Infrastructure.Tenancy;
using ClarioShop.SharedKernel.Security;
using Microsoft.AspNetCore.Identity;

namespace ClarioShop.Identity;

public static class IdentityModule
{
    /// <summary>Rate-limit policy the host must define for the sign-in endpoint.</summary>
    public const string TokenRateLimitPolicy = "identity-token";

    public static IServiceCollection AddIdentityModule(this IServiceCollection services)
    {
        services.AddTenantDbContext<IdentityDbContext>(IdentityDbContext.SchemaName);
        services.AddModuleHandlers(typeof(IdentityModule).Assembly);
        services.AddEndpointGroups(typeof(IdentityModule).Assembly);

        services.AddSingleton<IPermissionProvider, IdentityPermissionProvider>();
        services.AddSingleton<PermissionCatalog>();
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddSingleton<TokenService>();
        services.AddScoped<SystemRoleSynchronizer>();
        services.AddScoped<ITenantInitializer>(provider => provider.GetRequiredService<SystemRoleSynchronizer>());
        services.AddScoped<IIdentityProvisioning, IdentityProvisioning>();
        return services;
    }
}
