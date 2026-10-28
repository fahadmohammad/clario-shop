using ClarioShop.Catalog.Contracts;
using ClarioShop.Customers.Contracts;
using ClarioShop.Inventory.Contracts;
using ClarioShop.Ordering.Contracts.Events;
using ClarioShop.Ordering.Domain;
using ClarioShop.Ordering.Features.Carts;
using ClarioShop.Ordering.Features.Orders;
using ClarioShop.Ordering.Persistence;
using ClarioShop.Payments.Contracts;
using ClarioShop.Settings.Contracts;

namespace ClarioShop.Ordering.Features.Checkout;

/// <summary>
/// Checkout: prices the cart from Catalog, adds the delivery zone's fee and VAT from Settings, reserves the stock,
/// finds or creates the customer by phone and places the order. A cash-on-delivery order is Placed and waits for staff;
/// a prepaid one (SSLCommerz) is PendingPayment until Payments reports it paid. Checking out the same cart again
/// returns the order it already became. A signed-in shopper's order is always their own: it uses the phone number of
/// their account and may deliver to one of their saved addresses.
/// </summary>
public static class Checkout
{
    /// <param name="Phone">Required for guests. A signed-in shopper checks out with their own number: leave it out or send that number.</param>
    /// <param name="Name">Required for guests; a signed-in shopper defaults to their profile name, then the saved address's recipient.</param>
    /// <param name="Address">A delivery address typed in at checkout; send this or <paramref name="SavedAddressId"/>.</param>
    /// <param name="SavedAddressId">One of the signed-in shopper's saved addresses.</param>
    /// <param name="DeliveryZoneId">One of the store's active delivery zones; required unless the store has none.</param>
    /// <param name="PaymentMethod"><c>CashOnDelivery</c> (the default) or <c>SslCommerz</c> to pay up front online.</param>
    public sealed record Request(
        Guid CartId,
        string? Phone,
        string? Name,
        AddressInput? Address,
        Guid? SavedAddressId,
        Guid? DeliveryZoneId,
        string? Note,
        string? PaymentMethod = null);

    /// <param name="ShopperCustomerId">The signed-in shopper's customer id, from the token; null for a guest.</param>
    public sealed record Command(Request Request, Guid? ShopperCustomerId);

    public sealed record AddressInput(string Line1, string? Line2, string Area, string City, string? PostalCode);

    /// <param name="Created">False when the cart had already been checked out and its existing order is returned.</param>
    public sealed record Response(OrderDetails Order, bool Created);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.CartId).NotEmpty();

            // The Customers module decides what a valid Bangladeshi mobile number is; this only stops obvious garbage early.
            RuleFor(request => request.Phone).NotEmpty().MaximumLength(20).Matches(@"^\+?[0-9][0-9 \-]*$")
                .WithMessage("Enter a Bangladeshi mobile number, e.g. 01712-345678.")
                .When(request => request.Phone is not null);
            RuleFor(request => request.Name).NotEmpty().MaximumLength(200).When(request => request.Name is not null);
            RuleFor(request => request.Note).MaximumLength(500);
            RuleFor(request => request.PaymentMethod)
                .Must(method => ParsePaymentMethod(method) is not null)
                .WithMessage("Choose CashOnDelivery or SslCommerz.");
            RuleFor(request => request.Address).NotNull().When(request => request.SavedAddressId is null)
                .WithMessage("Enter a delivery address or choose a saved one.");
            RuleFor(request => request.Address).Null().When(request => request.SavedAddressId is not null)
                .WithMessage("Send a delivery address or a saved address, not both.");
            RuleFor(request => request.Address!).ChildRules(address =>
            {
                address.RuleFor(a => a.Line1).NotEmpty().MaximumLength(300);
                address.RuleFor(a => a.Line2).MaximumLength(300);
                address.RuleFor(a => a.Area).NotEmpty().MaximumLength(100);
                address.RuleFor(a => a.City).NotEmpty().MaximumLength(100);
                address.RuleFor(a => a.PostalCode).MaximumLength(10);
            }).When(request => request.Address is not null);
        }
    }

    internal sealed class Handler(
        OrderingDbContext db,
        ICatalogQueries catalog,
        ICustomerDirectory customers,
        IInventoryReservations reservations,
        ISettingsQueries settings,
        IPaymentMethods paymentMethods,
        TimeProvider clock) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            var cart = await db.FindCartAsync(command.Request.CartId, cancellationToken);
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

            var paymentMethod = ParsePaymentMethod(command.Request.PaymentMethod) ?? throw new ArgumentException("Validate the request first.", nameof(command));
            if (!await paymentMethods.IsAvailableAsync(paymentMethod.ToString(), cancellationToken))
            {
                return OrderingErrors.PaymentMethodUnavailable(paymentMethod.ToString());
            }

            var recipient = await RecipientAsync(command, cancellationToken);
            if (recipient.IsFailure)
            {
                return recipient.Error;
            }

            var delivery = await DeliveryAsync(command.Request.DeliveryZoneId, cancellationToken);
            if (delivery.IsFailure)
            {
                return delivery.Error;
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

            var placed = await PlaceAsync(cart, recipient.Value, command.Request.Note, orderId, priced.Value, delivery.Value, paymentMethod, reservation.Value.ReservationId, cancellationToken);
            if (placed.IsFailure || !placed.Value.Created)
            {
                await reservations.ReleaseAsync(reservation.Value.ReservationId, cancellationToken);
            }

            return placed;
        }

        /// <summary>
        /// Who the order is for and where it goes. A guest gives phone, name and address; a signed-in shopper's order uses
        /// their account's phone and may use one of their saved addresses, which are looked up by their customer id only.
        /// </summary>
        private async Task<Result<Recipient>> RecipientAsync(Command command, CancellationToken cancellationToken)
        {
            var request = command.Request;
            if (command.ShopperCustomerId is not { } shopperId)
            {
                if (request.SavedAddressId is not null)
                {
                    return OrderingErrors.SavedAddressNeedsSignIn;
                }

                if (string.IsNullOrWhiteSpace(request.Phone))
                {
                    return OrderingErrors.PhoneRequired;
                }

                return Optional(request.Name) is { } guestName
                    ? new Recipient(request.Phone, guestName, ToDeliveryAddress(request.Address!))
                    : OrderingErrors.NameRequired;
            }

            if (await customers.GetAsync(shopperId, cancellationToken) is not { } shopper)
            {
                return OrderingErrors.ShopperNotFound;
            }

            if (request.Phone is not null && customers.NormalizePhone(request.Phone) != shopper.Phone)
            {
                return OrderingErrors.NotShoppersPhone;
            }

            var saved = request.SavedAddressId is { } savedAddressId
                ? shopper.Addresses.FirstOrDefault(address => address.AddressId == savedAddressId)
                : null;
            if (request.SavedAddressId is not null && saved is null)
            {
                return OrderingErrors.SavedAddressNotFound;
            }

            var name = Optional(request.Name) ?? shopper.Name ?? saved?.RecipientName;
            if (name is null)
            {
                return OrderingErrors.NameRequired;
            }

            var deliverTo = saved is null
                ? ToDeliveryAddress(request.Address!)
                : new DeliveryAddress(saved.Line1, saved.Line2, saved.Area, saved.City, saved.PostalCode);
            return new Recipient(shopper.Phone, name, deliverTo);
        }

        /// <summary>The chosen zone as Settings prices it now; no zone is allowed only while the store has none.</summary>
        private async Task<Result<OrderDelivery?>> DeliveryAsync(Guid? deliveryZoneId, CancellationToken cancellationToken)
        {
            if (deliveryZoneId is not { } zoneId)
            {
                var zones = await settings.GetDeliveryZonesAsync(cancellationToken);
                return zones.Count == 0 ? Result.Success<OrderDelivery?>(null) : OrderingErrors.DeliveryZoneRequired;
            }

            return await settings.GetDeliveryZoneAsync(zoneId, cancellationToken) is { } zone
                ? new OrderDelivery(zone.Id, zone.Name, zone.Fee, zone.FreeDeliveryThreshold)
                : OrderingErrors.UnknownDeliveryZone;
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
            Recipient recipient,
            string? note,
            Guid orderId,
            IReadOnlyList<OrderLineSpec> lines,
            OrderDelivery? delivery,
            PaymentMethod paymentMethod,
            Guid reservationId,
            CancellationToken cancellationToken)
        {
            var customer = await customers.FindOrCreateByPhoneAsync(recipient.Phone, recipient.Name, cancellationToken);
            if (customer.IsFailure)
            {
                return customer.Error;
            }

            var vatRatePercent = (await settings.GetStoreSettingsAsync(cancellationToken)).VatRatePercent;
            var now = clock.GetUtcNow();
            var order = Order.Place(
                orderId,
                await db.NextOrderNumberAsync(cancellationToken),
                cart.Id,
                new OrderCustomer(customer.Value.CustomerId, customer.Value.Phone, recipient.Name),
                recipient.Address,
                note,
                lines,
                delivery,
                vatRatePercent,
                paymentMethod,
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
                order.Subtotal,
                order.DeliveryFee,
                order.Vat,
                order.Total,
                order.ReservationId,
                [.. order.Lines.OrderBy(line => line.Position).Select(line => new OrderPlacedLine(line.VariantId, line.Sku, line.Quantity, line.UnitPrice, line.LineTotal, line.ProductName, line.VariantName))]));

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

        private static DeliveryAddress ToDeliveryAddress(AddressInput address) => new(
            address.Line1.Trim(),
            Optional(address.Line2),
            address.Area.Trim(),
            address.City.Trim(),
            Optional(address.PostalCode));

        private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>The requested payment method; cash on delivery when none is given, null when it is not one we take.</summary>
    private static PaymentMethod? ParsePaymentMethod(string? method) =>
        method is null
            ? PaymentMethod.CashOnDelivery
            : Enum.GetValues<PaymentMethod>().Cast<PaymentMethod?>().FirstOrDefault(known => string.Equals(known.ToString(), method, StringComparison.OrdinalIgnoreCase));

    /// <param name="Phone">As given at checkout; the Customers module normalizes it.</param>
    private sealed record Recipient(string Phone, string Name, DeliveryAddress Address);
}
