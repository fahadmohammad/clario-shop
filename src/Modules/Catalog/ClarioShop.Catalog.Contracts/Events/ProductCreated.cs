using ClarioShop.SharedKernel.Messaging;

namespace ClarioShop.Catalog.Contracts.Events;

public sealed record ProductCreated(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid ProductId,
    string Name,
    string Slug,
    IReadOnlyList<ProductCreated.VariantInfo> Variants) : IIntegrationEvent
{
    public sealed record VariantInfo(Guid VariantId, string Sku, string Name, decimal Price);
}
