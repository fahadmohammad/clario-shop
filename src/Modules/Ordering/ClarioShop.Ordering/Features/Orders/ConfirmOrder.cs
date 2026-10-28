using ClarioShop.Inventory.Contracts;
using ClarioShop.Ordering.Contracts.Events;
using ClarioShop.Ordering.Domain;
using ClarioShop.Ordering.Persistence;

namespace ClarioShop.Ordering.Features.Orders;

/// <summary>
/// Staff confirm a cash-on-delivery order (usually after calling the shopper). The stock must still be held: if the
/// checkout reservation expired, the stock is reserved again first, and the confirm fails when it is gone.
/// Inventory turns the reservation into sale movements when it receives <see cref="OrderConfirmed"/>.
/// </summary>
public static class ConfirmOrder
{
    public sealed record Command(Guid OrderId, string? Note, Guid? UserId);

    internal sealed class Handler(OrderingDbContext db, IInventoryReservations reservations, TimeProvider clock)
        : ICommandHandler<Command, OrderDetails>
    {
        private const string InsufficientStock = "inventory.insufficient_stock";

        public async Task<Result<OrderDetails>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            var order = await db.LoadOrderAsync(command.OrderId, cancellationToken);
            if (order is null)
            {
                return OrderingErrors.OrderNotFound;
            }

            if (order.Status == OrderStatus.PendingPayment)
            {
                return OrderingErrors.AwaitingPayment;
            }

            if (!order.CanMoveTo(OrderStatus.Confirmed))
            {
                return OrderingErrors.InvalidTransition(order.Status, OrderStatus.Confirmed);
            }

            // Returns the order's live reservation, or reserves again when the checkout one ran out. A reservation made
            // here and not used (the save below loses a race) is harmless: it is the order's, and expires on its own.
            var held = await reservations.ReserveAsync(
                new ReserveStockRequest(order.Id, [.. order.Lines.Select(line => new ReservationLine(line.VariantId, line.Quantity))]),
                cancellationToken);
            if (held.IsFailure)
            {
                return held.Error.Code == InsufficientStock ? OrderingErrors.StockGone(held.Error.Message) : held.Error;
            }

            var now = clock.GetUtcNow();
            var confirmed = order.Confirm(held.Value.ReservationId, command.UserId, command.Note, now);
            if (confirmed.IsFailure)
            {
                return confirmed.Error;
            }

            db.Publish(new OrderConfirmed(
                Guid.CreateVersion7(),
                now,
                order.Id,
                OrderNumber.Format(order.Number),
                order.CustomerId,
                order.ReservationId));
            return await db.SaveOrderAsync(order, cancellationToken);
        }
    }
}
