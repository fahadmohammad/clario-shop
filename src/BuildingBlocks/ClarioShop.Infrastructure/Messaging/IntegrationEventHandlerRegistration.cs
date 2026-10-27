using ClarioShop.SharedKernel.Messaging;

namespace ClarioShop.Infrastructure.Messaging;

/// <summary>One handler subscribed to one event, owned by one module (its DbContext holds the inbox).</summary>
public sealed record IntegrationEventHandlerRegistration(
    string EventName,
    Type EventType,
    Type HandlerType,
    Type DbContextType,
    string QueueName,
    Func<IServiceProvider, IIntegrationEvent, CancellationToken, Task> InvokeAsync)
{
    public string HandlerName => HandlerType.FullName ?? HandlerType.Name;
}
