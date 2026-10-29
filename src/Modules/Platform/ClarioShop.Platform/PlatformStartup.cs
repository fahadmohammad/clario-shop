using ClarioShop.Infrastructure.Tenancy;
using ClarioShop.Platform.Domain;
using ClarioShop.Platform.Persistence;
using ClarioShop.Platform.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClarioShop.Platform;

public static partial class PlatformStartup
{
    /// <summary>
    /// Migrates the platform database and every tenant database, then seeds the demo tenant if enabled.
    /// Development runs it on startup; production runs it as a deploy step.
    /// </summary>
    public static async Task MigrateClarioDatabasesAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("ClarioShop.Startup");

        await provider.GetRequiredService<PlatformDbContext>().Database.MigrateAsync(cancellationToken);

        var migrator = provider.GetRequiredService<TenantMigrator>();
        foreach (var tenant in await provider.GetRequiredService<ITenantStore>().GetActiveTenantsAsync(cancellationToken))
        {
            try
            {
                await migrator.MigrateAsync(tenant, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogTenantMigrationFailed(logger, exception, tenant.Identifier);
            }
        }

        await SeedDemoTenantAsync(provider, logger, cancellationToken);
    }

    private static async Task SeedDemoTenantAsync(IServiceProvider provider, ILogger logger, CancellationToken cancellationToken)
    {
        var demo = provider.GetRequiredService<IOptions<PlatformOptions>>().Value.DemoTenant;
        if (!demo.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(demo.OwnerPassword))
        {
            LogDemoSkipped(logger);
            return;
        }

        var db = provider.GetRequiredService<PlatformDbContext>();
        if (await db.Tenants.AnyAsync(t => t.Identifier == demo.Identifier, cancellationToken))
        {
            return;
        }

        var result = await provider.GetRequiredService<TenantProvisioner>().ProvisionAsync(
            new TenantRequest(demo.Identifier, demo.Name, demo.Hosts, demo.OwnerEmail, "Demo Owner", demo.OwnerPassword, TenantPlan.RetailSuite),
            cancellationToken);

        if (result.IsSuccess)
        {
            LogDemoSeeded(logger, demo.Identifier, string.Join(", ", demo.Hosts));
        }
        else
        {
            LogDemoFailed(logger, result.Error.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Migrating tenant {Tenant} failed; it stays on its previous schema")]
    private static partial void LogTenantMigrationFailed(ILogger logger, Exception exception, string tenant);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Demo tenant not seeded: Platform:DemoTenant:OwnerPassword is empty")]
    private static partial void LogDemoSkipped(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded demo tenant {Tenant} at {Hosts}")]
    private static partial void LogDemoSeeded(ILogger logger, string tenant, string hosts);

    [LoggerMessage(Level = LogLevel.Error, Message = "Seeding the demo tenant failed: {Reason}")]
    private static partial void LogDemoFailed(ILogger logger, string reason);
}
