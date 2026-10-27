using ClarioShop.Infrastructure.Tenancy;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClarioShop.Inventory.Services;

/// <summary>Sweeps every tenant for expired reservations on a timer, so unpaid checkouts give their stock back.</summary>
internal sealed partial class ReservationExpiryService(
    IServiceScopeFactory scopeFactory,
    TenantScopeFactory tenantScopes,
    IOptions<InventoryOptions> options,
    TimeProvider clock,
    ILogger<ReservationExpiryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.ReservationSweepInterval, clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await SweepAllTenantsAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogSweepFailed(logger, exception);
            }
        }
    }

    private async Task SweepAllTenantsAsync(CancellationToken cancellationToken)
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
                var expiry = scope.ServiceProvider.GetRequiredService<ReservationExpiry>();
                var expired = await expiry.ExpireDueAsync(clock.GetUtcNow(), variantIds: null, cancellationToken);
                if (expired > 0)
                {
                    LogExpired(logger, expired, tenant.Identifier);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A concurrency clash with a checkout is fine: the next sweep picks the reservation up again.
                LogTenantFailed(logger, exception, tenant.Identifier);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Expired {Count} stock reservations for tenant {Tenant}")]
    private static partial void LogExpired(ILogger logger, int count, string tenant);

    [LoggerMessage(Level = LogLevel.Error, Message = "Reservation sweep failed")]
    private static partial void LogSweepFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reservation sweep failed for tenant {Tenant}")]
    private static partial void LogTenantFailed(ILogger logger, Exception exception, string tenant);
}
