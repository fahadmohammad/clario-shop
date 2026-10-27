using System.Text;
using RabbitMQ.Client;

namespace ClarioShop.Infrastructure.Messaging;

internal sealed class RabbitMqMessagePublisher(IConnection connection, IOptions<MessagingOptions> options) : IMessagePublisher
{
    public async Task PublishAsync(OutgoingMessage message, CancellationToken cancellationToken)
    {
        var exchange = options.Value.Exchange;
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: cancellationToken);

        var properties = new BasicProperties
        {
            MessageId = message.MessageId.ToString(),
            Type = message.EventType,
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            Headers = new Dictionary<string, object?> { [RabbitMqHeaders.TenantId] = message.TenantId.ToString() },
        };

        await channel.BasicPublishAsync(
            exchange,
            routingKey: message.EventType,
            mandatory: false,
            basicProperties: properties,
            body: Encoding.UTF8.GetBytes(message.Payload),
            cancellationToken: cancellationToken);
    }
}
