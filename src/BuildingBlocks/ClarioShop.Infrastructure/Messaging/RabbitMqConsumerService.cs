using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ClarioShop.Infrastructure.Messaging;

/// <summary>One durable queue per module; failed messages go to the dead-letter exchange instead of looping.</summary>
internal sealed partial class RabbitMqConsumerService(
    IConnection connection,
    IntegrationEventDispatcher dispatcher,
    IEnumerable<IntegrationEventHandlerRegistration> registrations,
    IOptions<MessagingOptions> options,
    ILogger<RabbitMqConsumerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var queues = registrations.GroupBy(r => r.QueueName).ToList();
        if (queues.Count == 0)
        {
            return;
        }

        var settings = options.Value;
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await channel.ExchangeDeclareAsync(settings.Exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: stoppingToken);
        await channel.ExchangeDeclareAsync(settings.DeadLetterExchange, ExchangeType.Fanout, durable: true, autoDelete: false, cancellationToken: stoppingToken);
        await channel.QueueDeclareAsync(settings.DeadLetterExchange, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
        await channel.QueueBindAsync(settings.DeadLetterExchange, settings.DeadLetterExchange, routingKey: string.Empty, cancellationToken: stoppingToken);
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 16, global: false, cancellationToken: stoppingToken);

        foreach (var queue in queues)
        {
            await channel.QueueDeclareAsync(
                queue.Key,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: new Dictionary<string, object?> { ["x-dead-letter-exchange"] = settings.DeadLetterExchange },
                cancellationToken: stoppingToken);

            foreach (var eventName in queue.Select(r => r.EventName).Distinct())
            {
                await channel.QueueBindAsync(queue.Key, settings.Exchange, eventName, cancellationToken: stoppingToken);
            }

            var consumer = new AsyncEventingBasicConsumer(channel);
            var queueName = queue.Key;
            consumer.ReceivedAsync += async (_, delivery) =>
            {
                try
                {
                    await dispatcher.DispatchAsync(ToMessage(delivery), queueName, stoppingToken);
                    await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, stoppingToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    LogFailed(logger, exception, delivery.BasicProperties.MessageId, queueName);
                    await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, stoppingToken);
                }
            };

            await channel.BasicConsumeAsync(queueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
        }

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Host is stopping.
        }
    }

    private static OutgoingMessage ToMessage(BasicDeliverEventArgs delivery)
    {
        var headers = delivery.BasicProperties.Headers
            ?? throw new InvalidOperationException("Message has no headers.");
        var tenantId = headers.TryGetValue(RabbitMqHeaders.TenantId, out var raw) && raw is byte[] bytes
            ? Guid.Parse(Encoding.UTF8.GetString(bytes))
            : throw new InvalidOperationException("Message has no tenant id.");

        return new OutgoingMessage(
            Guid.Parse(delivery.BasicProperties.MessageId ?? throw new InvalidOperationException("Message has no id.")),
            delivery.BasicProperties.Type ?? delivery.RoutingKey,
            Encoding.UTF8.GetString(delivery.Body.Span),
            tenantId);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Message {MessageId} failed in queue {Queue}; sent to dead letters")]
    private static partial void LogFailed(ILogger logger, Exception exception, string? messageId, string queue);
}
