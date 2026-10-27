using ClarioShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClarioShop.Infrastructure.Tenancy;

/// <summary>
/// Applies every module's migrations to one tenant database (creating the database if needed),
/// then runs the modules' tenant initializers.
/// </summary>
public sealed partial class TenantMigrator(
    TenantScopeFactory tenantScopes,
    IEnumerable<ModuleDbContextRegistration> modules,
    ILogger<TenantMigrator> logger)
{
    public async Task MigrateAsync(TenantInfo tenant, CancellationToken cancellationToken)
    {
        await using var scope = tenantScopes.CreateScope(tenant);

        foreach (var module in modules)
        {
            var context = (DbContext)scope.ServiceProvider.GetRequiredService(module.ContextType);
            await context.Database.MigrateAsync(cancellationToken);
            LogMigrated(logger, module.Schema, tenant.Identifier);
        }

        foreach (var initializer in scope.ServiceProvider.GetServices<ITenantInitializer>())
        {
            await initializer.InitializeAsync(cancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Migrated schema {Schema} for tenant {Tenant}")]
    private static partial void LogMigrated(ILogger logger, string schema, string tenant);
}
