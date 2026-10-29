using ClarioShop.SharedKernel.Results;

namespace ClarioShop.Inventory.Contracts;

/// <summary>
/// Read-only stock figures for reports, computed from Inventory's stock levels and ledger at the time of the call.
/// Only active locations are included. Fails with a validation error when <see cref="StockReportQuery.LocationId"/>
/// is not an active location.
/// </summary>
public interface IInventoryQueries
{
    /// <summary>
    /// What the stock on hand cost: each variant at the weighted average unit cost of everything received for it
    /// (all purchase receipts, all locations). Only stock above zero is valued (negative stock is reported by
    /// <see cref="GetNegativeStockAsync"/>); stock never received with a cost has no value and is counted as uncosted.
    /// </summary>
    Task<Result<StockValuation>> GetStockValuationAsync(StockReportQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// Stock whose available quantity (on hand less reserved) is at or below its reorder level, lowest first.
    /// Includes stock that has run out.
    /// </summary>
    Task<Result<StockAlertPage>> GetLowStockAsync(StockReportQuery query, CancellationToken cancellationToken);

    /// <summary>Stock with less than zero on hand (sold or written off beyond what the ledger knew of), lowest first.</summary>
    Task<Result<StockAlertPage>> GetNegativeStockAsync(StockReportQuery query, CancellationToken cancellationToken);
}

/// <param name="LocationId">One location, or every active location when null.</param>
public sealed record StockReportQuery(Guid? LocationId, int Page, int PageSize);

/// <param name="TotalValue">The value of all stock in scope, not just this page.</param>
/// <param name="UncostedItems">Rows with stock on hand but no receipt cost to value it at.</param>
/// <param name="Locations">The value per location in scope, by location code.</param>
/// <param name="Items">Rows with stock on hand, most valuable first; uncosted rows last.</param>
/// <param name="TotalCount">Rows with stock on hand in scope.</param>
public sealed record StockValuation(
    decimal TotalValue,
    int UncostedItems,
    IReadOnlyList<LocationStockValue> Locations,
    IReadOnlyList<StockValueItem> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record LocationStockValue(Guid LocationId, string LocationCode, decimal Value, int UncostedItems);

/// <param name="UnitCost">Weighted average receipt cost, rounded to the paisa; null when never received with a cost.</param>
/// <param name="Value">On hand × the unrounded average cost, rounded to the paisa; null when uncosted.</param>
public sealed record StockValueItem(
    Guid VariantId,
    string Sku,
    string ProductName,
    string VariantName,
    Guid LocationId,
    string LocationCode,
    decimal OnHand,
    decimal? UnitCost,
    decimal? Value);

public sealed record StockAlertPage(IReadOnlyList<StockAlertItem> Items, int Page, int PageSize, int TotalCount);

/// <param name="ReorderLevel">The variant's own reorder level, or the store-wide default when it has none.</param>
public sealed record StockAlertItem(
    Guid VariantId,
    string Sku,
    string ProductName,
    string VariantName,
    Guid LocationId,
    string LocationCode,
    decimal OnHand,
    decimal Reserved,
    decimal Available,
    decimal ReorderLevel);
