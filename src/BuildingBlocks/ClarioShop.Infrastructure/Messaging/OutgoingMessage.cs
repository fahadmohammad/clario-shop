namespace ClarioShop.Infrastructure.Messaging;

public sealed record OutgoingMessage(Guid MessageId, string EventType, string Payload, Guid TenantId);
