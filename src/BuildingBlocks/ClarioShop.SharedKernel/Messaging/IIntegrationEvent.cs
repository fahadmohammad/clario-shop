namespace ClarioShop.SharedKernel.Messaging;

/// <summary>An event published by one module for other modules. Pure data, versioned by type name.</summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }

    DateTimeOffset OccurredAt { get; }
}
