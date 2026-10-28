using ClarioShop.Infrastructure.Tenancy;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClarioShop.Payments.Features.Gateways;

/// <summary>Runs <see cref="GatewayReconciliation"/> for every store on a timer, catching gateway callbacks that never came.</summary>
internal sealed partial class GatewayReconciliationService(
    IServiceScopeFactory scopeFactory,
    TenantScopeFactory tenantScopes,
    IOptions<GatewayReconciliationOptions> options,
    TimeProvider clock,
    ILogger<GatewayReconciliationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.Interval, clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ReconcileAllTenantsAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogRunFailed(logger, exception);
            }
        }
    }

    private async Task ReconcileAllTenantsAsync(CancellationToken cancellationToken)
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
                var run = await scope.ServiceProvider.GetRequiredService<GatewayReconciliation>().ReconcileAsync(clock.GetUtcNow(), cancellationToken);
                if (run.Settled + run.Failed + run.Unavailable > 0)
                {
                    LogReconciled(logger, tenant.Identifier, run.Settled, run.Failed, run.Unavailable);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A clash with a callback for the same payment is fine: the next run looks at it again.
                LogTenantFailed(logger, exception, tenant.Identifier);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Payment reconciliation for tenant {Tenant}: {Settled} settled, {Failed} failed, {Unavailable} not checked")]
    private static partial void LogReconciled(ILogger logger, string tenant, int settled, int failed, int unavailable);

    [LoggerMessage(Level = LogLevel.Error, Message = "Payment reconciliation failed")]
    private static partial void LogRunFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Payment reconciliation failed for tenant {Tenant}")]
    private static partial void LogTenantFailed(ILogger logger, Exception exception, string tenant);
}
