using ClarioShop.SharedKernel.Messaging;

namespace ClarioShop.Catalog.Contracts.Events;

public sealed record ProductPublished(Guid EventId, DateTimeOffset OccurredAt, Guid ProductId, string Slug) : IIntegrationEvent;
