using ClarioShop.SharedKernel.Messaging;

namespace ClarioShop.Inventory.Contracts.Events;

/// <summary>
/// The drift check found a stock level that disagreed with the ledger and set it back to the ledger's figures.
/// Points at a bug or a manual database change; the stock shown to shoppers was wrong until now.
/// </summary>
/// <param name="CheckId">The drift check run that found it.</param>
/// <param name="RecordedOnHand">What the stock level said.</param>
/// <param name="LedgerOnHand">The sum of the ledger's movements, which the level now shows.</param>
/// <param name="RecordedReserved">What the stock level said was reserved.</param>
/// <param name="HeldReserved">What active reservations hold, which the level now shows.</param>
public sealed record StockDriftDetected(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid CheckId,
    Guid VariantId,
    string Sku,
    Guid LocationId,
    string LocationCode,
    decimal RecordedOnHand,
    decimal LedgerOnHand,
    decimal RecordedReserved,
    decimal HeldReserved) : IIntegrationEvent;
