using ClarioShop.SharedKernel.Messaging;

namespace ClarioShop.Catalog.Contracts.Events;

/// <summary>A product left the storefront for good; its variants are no longer sellable.</summary>
public sealed record ProductArchived(Guid EventId, DateTimeOffset OccurredAt, Guid ProductId) : IIntegrationEvent;
