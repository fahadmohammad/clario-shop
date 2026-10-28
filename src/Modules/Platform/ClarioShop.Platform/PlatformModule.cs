using ClarioShop.Infrastructure.Handlers;
using ClarioShop.Infrastructure.Tenancy;
using ClarioShop.Platform.Persistence;
using ClarioShop.Platform.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ClarioShop.Platform;

public static class PlatformModule
{
    public const string ConnectionName = "platformdb";

    public static IServiceCollection AddPlatformModule(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var section = configuration.GetSection(PlatformOptions.SectionName);
        services.Configure<PlatformOptions>(section);

        var connectionString = configuration.GetConnectionString(ConnectionName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionName}' is missing.");
        services.AddDbContext<PlatformDbContext>(options => options.UseNpgsql(connectionString));
        services.AddPlatformDataProtection(section.Get<PlatformOptions>() ?? new PlatformOptions(), environment);

        services.AddModuleHandlers(typeof(PlatformModule).Assembly);
        services.AddEndpointGroups(typeof(PlatformModule).Assembly);
        services.AddScoped<ITenantStore, TenantStore>();
        services.AddScoped<TenantProvisioner>();
        return services;
    }
}
