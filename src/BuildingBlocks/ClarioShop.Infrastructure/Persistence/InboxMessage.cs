namespace ClarioShop.Infrastructure.Persistence;

/// <summary>Records that a handler has processed a message, so redelivery does not run it twice.</summary>
public sealed class InboxMessage
{
    public Guid MessageId { get; init; }

    public required string Handler { get; init; }

    public DateTimeOffset ProcessedAt { get; init; }
}
