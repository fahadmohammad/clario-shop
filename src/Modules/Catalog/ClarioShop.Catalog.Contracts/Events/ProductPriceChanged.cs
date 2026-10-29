using ClarioShop.SharedKernel.Messaging;

namespace ClarioShop.Catalog.Contracts.Events;

/// <summary>A variant's price or compare-at price changed. Orders already placed keep the price they copied.</summary>
public sealed record ProductPriceChanged(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid ProductId,
    Guid VariantId,
    string Sku,
    decimal OldPrice,
    decimal NewPrice,
    decimal? OldCompareAtPrice,
    decimal? NewCompareAtPrice) : IIntegrationEvent;
