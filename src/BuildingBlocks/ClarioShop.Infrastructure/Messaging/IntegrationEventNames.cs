namespace ClarioShop.Infrastructure.Messaging;

/// <summary>The wire name of an event type, used as the RabbitMQ routing key. Renaming an event type is a breaking change.</summary>
public static class IntegrationEventNames
{
    public static string For(Type eventType) =>
        eventType.FullName ?? throw new ArgumentException("Integration events must be named types.", nameof(eventType));
}
