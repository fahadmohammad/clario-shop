using ClarioShop.SharedKernel.Messaging;

namespace ClarioShop.Catalog.Contracts.Events;

/// <summary>A variant was added to a product after it was created (variants created with it come in <see cref="ProductCreated"/>).</summary>
public sealed record VariantAdded(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid ProductId,
    string ProductName,
    Guid VariantId,
    string Sku,
    string Name,
    decimal Price) : IIntegrationEvent;
