namespace ClarioShop.Catalog.Contracts;

/// <summary>Synchronous reads other modules may use (e.g. Ordering pricing a cart). Read-only.</summary>
public interface ICatalogQueries
{
    Task<IReadOnlyList<VariantSummary>> GetVariantsAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken cancellationToken);
}

/// <param name="SoldByWeight">Priced per kg; quantities may be fractional.</param>
public sealed record VariantSummary(
    Guid VariantId,
    Guid ProductId,
    string ProductName,
    string Sku,
    string Name,
    decimal Price,
    bool IsSellable,
    bool SoldByWeight);
