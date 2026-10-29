using ClarioShop.SharedKernel.Messaging;

namespace ClarioShop.Inventory.Contracts.Events;

/// <summary>
/// A variant's on-hand stock at a location dropped below zero: a sale was recorded that the ledger had no stock
/// for, so the shelf count is wrong somewhere. Published once when it crosses zero, not again while it stays below.
/// </summary>
/// <param name="OnHand">On-hand stock after the change (below zero).</param>
public sealed record StockWentNegative(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid VariantId,
    string Sku,
    string ProductName,
    Guid LocationId,
    string LocationCode,
    decimal OnHand) : IIntegrationEvent;
