using ClarioShop.Infrastructure.Tenancy;
using ClarioShop.Notifications.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClarioShop.Notifications.Features.Retries;

/// <summary>Runs <see cref="NotificationRetry"/> for every store on a timer.</summary>
internal sealed partial class NotificationRetryService(
    IServiceScopeFactory scopeFactory,
    TenantScopeFactory tenantScopes,
    IOptions<NotificationOptions> options,
    TimeProvider clock,
    ILogger<NotificationRetryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.RetryInterval, clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RetryAllTenantsAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogRunFailed(logger, exception);
            }
        }
    }

    private async Task RetryAllTenantsAsync(CancellationToken cancellationToken)
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
                var tried = await scope.ServiceProvider.GetRequiredService<NotificationRetry>().RetryDueAsync(clock.GetUtcNow(), cancellationToken);
                if (tried > 0)
                {
                    LogRetried(logger, tenant.Identifier, tried);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogTenantFailed(logger, exception, tenant.Identifier);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Notification retries for tenant {Tenant}: {Tried} tried")]
    private static partial void LogRetried(ILogger logger, string tenant, int tried);

    [LoggerMessage(Level = LogLevel.Error, Message = "Notification retries failed")]
    private static partial void LogRunFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Notification retries failed for tenant {Tenant}")]
    private static partial void LogTenantFailed(ILogger logger, Exception exception, string tenant);
}
