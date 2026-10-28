using ClarioShop.Catalog.Contracts;
using ClarioShop.Customers.Contracts;
using ClarioShop.Inventory.Contracts;
using ClarioShop.Ordering.Contracts.Events;
using ClarioShop.Ordering.Domain;
using ClarioShop.Ordering.Features.Carts;
using ClarioShop.Ordering.Features.Orders;
using ClarioShop.Ordering.Persistence;

namespace ClarioShop.Ordering.Features.Checkout;

/// <summary>
/// Cash-on-delivery checkout: prices the cart from Catalog, reserves the stock, finds or creates the customer by
/// phone and places the order. Checking out the same cart again returns the order it already became.
/// </summary>
public static class Checkout
{
    public sealed record Command(Guid CartId, string Phone, string Name, AddressInput Address, string? Note);

    public sealed record AddressInput(string Line1, string? Line2, string Area, string City, string? PostalCode);

    /// <param name="Created">False when the cart had already been checked out and its existing order is returned.</param>
    public sealed record Response(OrderDetails Order, bool Created);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.CartId).NotEmpty();

            // The Customers module decides what a valid Bangladeshi mobile number is; this only stops obvious garbage early.
            RuleFor(command => command.Phone).NotEmpty().MaximumLength(20).Matches(@"^\+?[0-9][0-9 \-]*$")
                .WithMessage("Enter a Bangladeshi mobile number, e.g. 01712-345678.");
            RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
            RuleFor(command => command.Note).MaximumLength(500);
            RuleFor(command => command.Address).NotNull().ChildRules(address =>
            {
                address.RuleFor(a => a.Line1).NotEmpty().MaximumLength(300);
                address.RuleFor(a => a.Line2).MaximumLength(300);
                address.RuleFor(a => a.Area).NotEmpty().MaximumLength(100);
                address.RuleFor(a => a.City).NotEmpty().MaximumLength(100);
                address.RuleFor(a => a.PostalCode).MaximumLength(10);
            });
        }
    }

    internal sealed class Handler(
        OrderingDbContext db,
        ICatalogQueries catalog,
        ICustomerDirectory customers,
        IInventoryReservations reservations,
        TimeProvider clock) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            var cart = await db.FindCartAsync(command.CartId, cancellationToken);
            if (cart is null)
            {
                return OrderingErrors.CartNotFound;
            }

            if (cart.OrderId is { } placedOrderId)
            {
                return await ExistingOrderAsync(placedOrderId, cancellationToken);
            }

            if (cart.Items.Count == 0)
            {
                return OrderingErrors.CartEmpty;
            }

            var priced = await PriceAsync(cart, cancellationToken);
            if (priced.IsFailure)
            {
                return priced.Error;
            }

            // The order id is chosen first so the stock can be held for it before anything is written here.
            var orderId = Guid.CreateVersion7();
            var reservation = await reservations.ReserveAsync(
                new ReserveStockRequest(orderId, [.. priced.Value.Select(line => new ReservationLine(line.VariantId, line.Quantity))]),
                cancellationToken);
            if (reservation.IsFailure)
            {
                // A concurrent checkout of this cart may have taken the last units; then its order is the answer.
                return await CheckedOutOrderAsync(cart.Id, cancellationToken) ?? Result.Failure<Response>(reservation.Error);
            }

            var placed = await PlaceAsync(cart, command, orderId, priced.Value, reservation.Value.ReservationId, cancellationToken);
            if (placed.IsFailure || !placed.Value.Created)
            {
                await reservations.ReleaseAsync(reservation.Value.ReservationId, cancellationToken);
            }

            return placed;
        }

        /// <summary>The cart's lines at the catalog's current prices; fails when any item is no longer for sale.</summary>
        private async Task<Result<List<OrderLineSpec>>> PriceAsync(Cart cart, CancellationToken cancellationToken)
        {
            var variants = (await catalog.GetVariantsAsync([.. cart.Items.Select(item => item.VariantId)], cancellationToken))
                .ToDictionary(variant => variant.VariantId);
            var items = cart.Items.OrderBy(item => item.AddedAt).ThenBy(item => item.Id).ToList();

            var unavailable = items
                .Where(item => variants.GetValueOrDefault(item.VariantId) is not { IsSellable: true })
                .Select(item => variants.GetValueOrDefault(item.VariantId)?.Sku ?? item.VariantId.ToString())
                .ToList();
            if (unavailable.Count > 0)
            {
                return OrderingErrors.ItemsUnavailable(unavailable);
            }

            return items
                .Select(item =>
                {
                    var variant = variants[item.VariantId];
                    return new OrderLineSpec(variant.VariantId, variant.ProductId, variant.ProductName, variant.Name, variant.Sku, variant.Price, item.Quantity);
                })
                .ToList();
        }

        private async Task<Result<Response>> PlaceAsync(
            Cart cart,
            Command command,
            Guid orderId,
            IReadOnlyList<OrderLineSpec> lines,
            Guid reservationId,
            CancellationToken cancellationToken)
        {
            var customer = await customers.FindOrCreateByPhoneAsync(command.Phone, command.Name, cancellationToken);
            if (customer.IsFailure)
            {
                return customer.Error;
            }

            var now = clock.GetUtcNow();
            var order = Order.PlaceCashOnDelivery(
                orderId,
                await db.NextOrderNumberAsync(cancellationToken),
                cart.Id,
                new OrderCustomer(customer.Value.CustomerId, customer.Value.Phone, command.Name.Trim()),
                new DeliveryAddress(
                    command.Address.Line1.Trim(),
                    Optional(command.Address.Line2),
                    command.Address.Area.Trim(),
                    command.Address.City.Trim(),
                    Optional(command.Address.PostalCode)),
                command.Note,
                lines,
                reservationId,
                now);

            var checkedOut = cart.CheckOut(order.Id, now);
            if (checkedOut.IsFailure)
            {
                return checkedOut.Error;
            }

            db.Orders.Add(order);
            db.Publish(new OrderPlaced(
                Guid.CreateVersion7(),
                now,
                order.Id,
                OrderNumber.Format(order.Number),
                order.CustomerId,
                order.PaymentMethod.ToString(),
                order.Total,
                order.ReservationId,
                [.. order.Lines.OrderBy(line => line.Position).Select(line => new OrderPlacedLine(line.VariantId, line.Sku, line.Quantity, line.UnitPrice))]));

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.IsRace())
            {
                // Another checkout of this cart won (unique cart id on orders), or the cart changed under us.
                db.ChangeTracker.Clear();
                return await CheckedOutOrderAsync(cart.Id, cancellationToken) ?? Result.Failure<Response>(OrderingErrors.CartChanged);
            }

            return new Response(OrderDetails.From(order), Created: true);
        }

        private async Task<Result<Response>?> CheckedOutOrderAsync(Guid cartId, CancellationToken cancellationToken)
        {
            var orderId = await db.Carts.AsNoTracking()
                .Where(cart => cart.Id == cartId)
                .Select(cart => cart.OrderId)
                .SingleAsync(cancellationToken);
            return orderId is { } placed ? await ExistingOrderAsync(placed, cancellationToken) : null;
        }

        private async Task<Result<Response>> ExistingOrderAsync(Guid orderId, CancellationToken cancellationToken) =>
            await db.FindOrderAsync(orderId, cancellationToken) is { } order
                ? new Response(OrderDetails.From(order), Created: false)
                : OrderingErrors.OrderNotFound;

        private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
