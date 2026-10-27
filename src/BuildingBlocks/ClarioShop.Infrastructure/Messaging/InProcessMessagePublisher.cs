namespace ClarioShop.Infrastructure.Messaging;

/// <summary>Delivers messages straight to local handlers. Used when no RabbitMQ is configured (tests, quick local runs).</summary>
internal sealed class InProcessMessagePublisher(IntegrationEventDispatcher dispatcher) : IMessagePublisher
{
    public Task PublishAsync(OutgoingMessage message, CancellationToken cancellationToken) =>
        dispatcher.DispatchAsync(message, queueName: null, cancellationToken);
}
