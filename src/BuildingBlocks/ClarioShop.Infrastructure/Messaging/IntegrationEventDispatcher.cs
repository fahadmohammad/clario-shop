using System.Text.Json;
using ClarioShop.Infrastructure.Persistence;
using ClarioShop.Infrastructure.Tenancy;
using ClarioShop.SharedKernel.Messaging;
using Microsoft.EntityFrameworkCore;

namespace ClarioShop.Infrastructure.Messaging;

/// <summary>Runs the handlers for an incoming message inside its tenant, once per handler (inbox).</summary>
public sealed partial class IntegrationEventDispatcher(
    IServiceScopeFactory scopeFactory,
    TenantScopeFactory tenantScopes,
    IEnumerable<IntegrationEventHandlerRegistration> registrations,
    TimeProvider clock,
    ILogger<IntegrationEventDispatcher> logger)
{
    public async Task DispatchAsync(OutgoingMessage message, string? queueName, CancellationToken cancellationToken)
    {
        var handlers = registrations
            .Where(r => r.EventName == message.EventType && (queueName is null || r.QueueName == queueName))
            .ToList();

        if (handlers.Count == 0)
        {
            return;
        }

        TenantInfo? tenant;
        await using (var lookupScope = scopeFactory.CreateAsyncScope())
        {
            tenant = await lookupScope.ServiceProvider.GetRequiredService<ITenantStore>()
                .FindByIdAsync(message.TenantId, cancellationToken);
        }

        if (tenant is null)
        {
            LogUnknownTenant(logger, message.MessageId, message.TenantId);
            return;
        }

        foreach (var handler in handlers)
        {
            await using var scope = tenantScopes.CreateScope(tenant);
            var db = (ModuleDbContext)scope.ServiceProvider.GetRequiredService(handler.DbContextType);

            var alreadyHandled = await db.InboxMessages.AnyAsync(
                inbox => inbox.MessageId == message.MessageId && inbox.Handler == handler.HandlerName,
                cancellationToken);
            if (alreadyHandled)
            {
                continue;
            }

            var integrationEvent = (IIntegrationEvent)(JsonSerializer.Deserialize(message.Payload, handler.EventType, JsonSerializerOptions.Web)
                ?? throw new InvalidOperationException($"Message {message.MessageId} has an empty payload."));

            await handler.InvokeAsync(scope.ServiceProvider, integrationEvent, cancellationToken);

            db.InboxMessages.Add(new InboxMessage
            {
                MessageId = message.MessageId,
                Handler = handler.HandlerName,
                ProcessedAt = clock.GetUtcNow(),
            });
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Dropping message {MessageId}: tenant {TenantId} not found")]
    private static partial void LogUnknownTenant(ILogger logger, Guid messageId, Guid tenantId);
}
