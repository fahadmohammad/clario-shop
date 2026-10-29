using ClarioShop.Infrastructure.Tenancy;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClarioShop.Inventory.Services;

/// <summary>Runs the drift check for every tenant once a night, and warns about every tenant whose stock drifted.</summary>
internal sealed partial class StockDriftCheckService(
    IServiceScopeFactory scopeFactory,
    TenantScopeFactory tenantScopes,
    IOptions<InventoryOptions> options,
    TimeProvider clock,
    ILogger<StockDriftCheckService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.DriftCheckEnabled)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = clock.GetUtcNow();
            await Task.Delay(DriftSchedule.NextRunAfter(now, options.Value.DriftCheckAtUtc) - now, clock, stoppingToken);
            try
            {
                await CheckAllTenantsAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogCheckFailed(logger, exception);
            }
        }
    }

    private async Task CheckAllTenantsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<TenantInfo> tenants;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            tenants = await scope.ServiceProvider.GetRequiredService<ITenantStore>().GetActiveTenantsAsync(cancellationToken);
        }

        foreach (var tenant in tenants)
        {
            try
            {
                await using var scope = tenantScopes.CreateScope(tenant);
                var outcome = await scope.ServiceProvider.GetRequiredService<StockDriftCheck>().RunAsync(cancellationToken);
                if (outcome.IsFailure)
                {
                    LogTenantSkipped(logger, tenant.Identifier, outcome.Error.Message);
                }
                else if (outcome.Value.Drifts.Count > 0)
                {
                    LogDrifted(logger, outcome.Value.Drifts.Count, tenant.Identifier, outcome.Value.CheckId);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogTenantFailed(logger, exception, tenant.Identifier);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Drift check corrected {Count} stock levels for tenant {Tenant} (check {CheckId})")]
    private static partial void LogDrifted(ILogger logger, int count, string tenant, Guid checkId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Drift check skipped tenant {Tenant}: {Reason}")]
    private static partial void LogTenantSkipped(ILogger logger, string tenant, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Drift check failed")]
    private static partial void LogCheckFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Drift check failed for tenant {Tenant}")]
    private static partial void LogTenantFailed(ILogger logger, Exception exception, string tenant);
}
