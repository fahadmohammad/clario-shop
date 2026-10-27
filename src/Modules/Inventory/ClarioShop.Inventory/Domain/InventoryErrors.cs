using ClarioShop.SharedKernel.Results;

namespace ClarioShop.Inventory.Domain;

internal static class InventoryErrors
{
    public static Error InsufficientStock(Guid variantId, decimal available, decimal requested) =>
        Error.Conflict(
            "inventory.insufficient_stock",
            $"Variant {variantId} has {available:0.###} available, {requested:0.###} requested.");

    public static readonly Error ReservationNotFound =
        Error.NotFound("inventory.reservation_not_found", "The reservation does not exist.");

    public static readonly Error ReservationExpired =
        Error.Conflict("inventory.reservation_expired", "The reservation has expired and its stock was released.");

    public static readonly Error ReservationReleased =
        Error.Conflict("inventory.reservation_released", "The reservation was released and can no longer be committed.");

    public static readonly Error ReservationCommitted =
        Error.Conflict("inventory.reservation_committed", "The reservation was already turned into a sale.");

    public static readonly Error LocationNotFound =
        Error.Validation("locationId", "The location does not exist or is inactive.");

    public static readonly Error Concurrency =
        Error.Conflict("inventory.concurrency", "Stock changed while saving; please try again.");

    public static Error UnknownVariants(IEnumerable<Guid> variantIds) =>
        Error.Validation("lines", $"Unknown variants: {string.Join(", ", variantIds)}.");
}
