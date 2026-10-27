namespace ClarioShop.Api.Setup;

internal static class CorsSetup
{
    /// <summary>Explicit origins only (storefront and back-office hosts), from Cors:Origins.</summary>
    public static IServiceCollection AddClarioCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
        return services.AddCors(options => options.AddDefaultPolicy(policy =>
            policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));
    }
}
