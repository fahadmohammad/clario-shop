using System.Globalization;
using ClarioShop.Infrastructure.Persistence;
using ClarioShop.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace ClarioShop.Infrastructure.Messaging;

/// <summary>
/// Polls every tenant's module outboxes and publishes pending messages. Rows are locked with
/// FOR UPDATE SKIP LOCKED, so several API instances can run this safely side by side.
/// </summary>
internal sealed partial class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    TenantScopeFactory tenantScopes,
    IEnumerable<ModuleDbContextRegistration> modules,
    IOptions<MessagingOptions> options,
    TimeProvider clock,
    ILogger<OutboxProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.OutboxPollInterval, clock);
        do
        {
            try
            {
                await ProcessAllTenantsAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogCycleFailed(logger, exception);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    internal async Task ProcessAllTenantsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<TenantInfo> tenants;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            tenants = await scope.ServiceProvider.GetRequiredService<ITenantStore>().GetActiveTenantsAsync(cancellationToken);
        }

        foreach (var tenant in tenants)
        {
            foreach (var module in modules)
            {
                try
                {
                    await ProcessAsync(tenant, module, cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    LogModuleFailed(logger, exception, module.Schema, tenant.Identifier);
                }
            }
        }
    }

    private async Task ProcessAsync(TenantInfo tenant, ModuleDbContextRegistration module, CancellationToken cancellationToken)
    {
        await using var scope = tenantScopes.CreateScope(tenant);
        var db = (ModuleDbContext)scope.ServiceProvider.GetRequiredService(module.ContextType);
        var publisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();
        var settings = options.Value;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var sql = string.Format(
            CultureInfo.InvariantCulture,
            """
            SELECT * FROM "{0}"."OutboxMessages"
            WHERE "ProcessedAt" IS NULL AND "Attempts" < {1}
            ORDER BY "OccurredAt"
            LIMIT {2}
            FOR UPDATE SKIP LOCKED
            """,
            module.Schema,
            settings.MaxAttempts,
            settings.OutboxBatchSize);

        var messages = await db.OutboxMessages.FromSqlRaw(sql).ToListAsync(cancellationToken);
        if (messages.Count == 0)
        {
            return;
        }

        foreach (var message in messages)
        {
            try
            {
                await publisher.PublishAsync(
                    new OutgoingMessage(message.Id, message.Type, message.Content, tenant.Id),
                    cancellationToken);
                message.ProcessedAt = clock.GetUtcNow();
                message.Error = null;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                message.Attempts++;
                message.Error = exception.Message.Length > 2000 ? exception.Message[..2000] : exception.Message;
                LogPublishFailed(logger, exception, message.Id, tenant.Identifier);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox cycle failed")]
    private static partial void LogCycleFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox of {Schema} failed for tenant {Tenant}")]
    private static partial void LogModuleFailed(ILogger logger, Exception exception, string schema, string tenant);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Publishing outbox message {MessageId} for tenant {Tenant} failed")]
    private static partial void LogPublishFailed(ILogger logger, Exception exception, Guid messageId, string tenant);
}
