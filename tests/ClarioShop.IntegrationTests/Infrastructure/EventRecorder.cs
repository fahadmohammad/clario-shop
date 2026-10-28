using System.Collections.Concurrent;
using ClarioShop.Catalog.Contracts.Events;
using ClarioShop.Customers.Contracts.Events;
using ClarioShop.Ordering.Contracts.Events;
using ClarioShop.SharedKernel.Messaging;
using ClarioShop.SharedKernel.Tenancy;

namespace ClarioShop.IntegrationTests.Infrastructure;

/// <summary>Collects events delivered through the outbox so tests can assert on cross-module messaging.</summary>
public sealed class EventRecorder
{
    private readonly ConcurrentQueue<(Guid TenantId, IIntegrationEvent Event)> received = new();

    public void Record(Guid tenantId, IIntegrationEvent integrationEvent) => received.Enqueue((tenantId, integrationEvent));

    public async Task<TEvent> WaitForAsync<TEvent>(Func<TEvent, bool> match, TimeSpan timeout)
        where TEvent : IIntegrationEvent
    {
        using var cancellation = new CancellationTokenSource(timeout);
        while (true)
        {
            var found = received.Select(item => item.Event).OfType<TEvent>().FirstOrDefault(match);
            if (found is not null)
            {
                return found;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellation.Token);
        }
    }

    public int Count<TEvent>(Func<TEvent, bool> match) => received.Select(item => item.Event).OfType<TEvent>().Count(match);

    public Guid TenantOf(IIntegrationEvent integrationEvent) => received.First(item => item.Event.EventId == integrationEvent.EventId).TenantId;
}

internal sealed class RecordingProductCreatedHandler(EventRecorder recorder, ITenantContext tenant) : IIntegrationEventHandler<ProductCreated>
{
    public Task HandleAsync(ProductCreated integrationEvent, CancellationToken cancellationToken)
    {
        recorder.Record(tenant.TenantId, integrationEvent);
        return Task.CompletedTask;
    }
}

internal sealed class RecordingCustomerCreatedHandler(EventRecorder recorder, ITenantContext tenant) : IIntegrationEventHandler<CustomerCreated>
{
    public Task HandleAsync(CustomerCreated integrationEvent, CancellationToken cancellationToken)
    {
        recorder.Record(tenant.TenantId, integrationEvent);
        return Task.CompletedTask;
    }
}

internal sealed class RecordingOrderPlacedHandler(EventRecorder recorder, ITenantContext tenant) : IIntegrationEventHandler<OrderPlaced>
{
    public Task HandleAsync(OrderPlaced integrationEvent, CancellationToken cancellationToken)
    {
        recorder.Record(tenant.TenantId, integrationEvent);
        return Task.CompletedTask;
    }
}

internal sealed class RecordingHandler<TEvent>(EventRecorder recorder, ITenantContext tenant) : IIntegrationEventHandler<TEvent>
    where TEvent : IIntegrationEvent
{
    public Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken)
    {
        recorder.Record(tenant.TenantId, integrationEvent);
        return Task.CompletedTask;
    }
}
