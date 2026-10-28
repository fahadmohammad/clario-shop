using ClarioShop.Ordering.Contracts.Events;
using ClarioShop.Ordering.Domain;
using ClarioShop.Ordering.Persistence;
using ClarioShop.SharedKernel.Messaging;

namespace ClarioShop.Ordering.Features.Orders;

/// <summary>
/// Moves an order through packing, shipping and delivery, or out of the lifecycle (cancelled, returned).
/// Confirming is <see cref="ConfirmOrder"/>, because it must make sure the stock is still held.
/// </summary>
public static class ChangeOrderStatus
{
    internal sealed record Command(Guid OrderId, OrderStatus Target, string? Note, Guid? UserId);

    internal sealed class Handler(OrderingDbContext db, TimeProvider clock) : ICommandHandler<Command, OrderDetails>
    {
        public async Task<Result<OrderDetails>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            if (command.Target == OrderStatus.Confirmed)
            {
                throw new ArgumentException("Confirm through ConfirmOrder.", nameof(command));
            }

            var order = await db.LoadOrderAsync(command.OrderId, cancellationToken);
            if (order is null)
            {
                return OrderingErrors.OrderNotFound;
            }

            var previous = order.Status;
            var now = clock.GetUtcNow();
            var moved = order.MoveTo(command.Target, command.UserId, command.Note, now);
            if (moved.IsFailure)
            {
                return moved.Error;
            }

            if (EventFor(order, previous, command.Note, now) is { } integrationEvent)
            {
                db.Publish(integrationEvent);
            }

            return await db.SaveOrderAsync(order, cancellationToken);
        }

        private static IIntegrationEvent? EventFor(Order order, OrderStatus previous, string? note, DateTimeOffset now)
        {
            var number = OrderNumber.Format(order.Number);
            var reason = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            return order.Status switch
            {
                OrderStatus.Cancelled => new OrderCancelled(
                    Guid.CreateVersion7(), now, order.Id, number, order.CustomerId, order.ReservationId, previous.ToString(), reason),
                OrderStatus.Packed => new OrderPacked(Guid.CreateVersion7(), now, order.Id, number, order.CustomerId),
                OrderStatus.Shipped => new OrderShipped(Guid.CreateVersion7(), now, order.Id, number, order.CustomerId),
                OrderStatus.Delivered => new OrderDelivered(Guid.CreateVersion7(), now, order.Id, number, order.CustomerId),

                // Restocking a return is decided with return requests in a later step; until then nothing consumes this but reports.
                OrderStatus.Returned => new OrderReturned(Guid.CreateVersion7(), now, order.Id, number, order.CustomerId, previous.ToString(), reason),
                _ => null,
            };
        }
    }
}
