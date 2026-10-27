namespace ClarioShop.Infrastructure.Messaging;

/// <summary>Transport for outbox messages: RabbitMQ in deployed environments, in-process in tests.</summary>
public interface IMessagePublisher
{
    Task PublishAsync(OutgoingMessage message, CancellationToken cancellationToken);
}
