using Microsoft.AspNetCore.Authorization;

namespace ClarioShop.Infrastructure.Security;

public static class PermissionRegistration
{
    public static IServiceCollection AddPermissionAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options => options.AddPolicy(
            ShopperRequirement.PolicyName,
            policy => policy.RequireAuthenticatedUser().AddRequirements(new ShopperRequirement())));
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, ShopperAuthorizationHandler>();
        return services;
    }
}
