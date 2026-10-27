using ClarioShop.Infrastructure.Handlers;
using ClarioShop.Infrastructure.Tenancy;
using ClarioShop.Platform.Persistence;
using ClarioShop.Platform.Services;
using Microsoft.Extensions.Configuration;

namespace ClarioShop.Platform;

public static class PlatformModule
{
    public const string ConnectionName = "platformdb";

    public static IServiceCollection AddPlatformModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PlatformOptions>(configuration.GetSection(PlatformOptions.SectionName));

        var connectionString = configuration.GetConnectionString(ConnectionName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionName}' is missing.");
        services.AddDbContext<PlatformDbContext>(options => options.UseNpgsql(connectionString));

        services.AddModuleHandlers(typeof(PlatformModule).Assembly);
        services.AddEndpointGroups(typeof(PlatformModule).Assembly);
        services.AddScoped<ITenantStore, TenantStore>();
        services.AddScoped<TenantProvisioner>();
        return services;
    }
}
