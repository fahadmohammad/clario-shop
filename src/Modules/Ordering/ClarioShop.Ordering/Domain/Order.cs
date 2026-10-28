using ClarioShop.SharedKernel.Domain;
using ClarioShop.SharedKernel.Results;

namespace ClarioShop.Ordering.Domain;

/// <summary>
/// A shopper's order. Lines copy name, SKU and price at checkout; the stock it needs is held by an
/// Inventory reservation; every status change is written to its history.
/// </summary>
internal sealed class Order : Entity
{
    private readonly List<OrderLine> lines = [];

    private readonly List<OrderStatusChange> history = [];

    private readonly List<OrderPayment> payments = [];

    private Order()
    {
    }

    /// <summary>Per-tenant sequence value; shown as <see cref="OrderNumber.Format"/>.</summary>
    public long Number { get; private set; }

    /// <summary>The cart checked out into this order; one order per cart.</summary>
    public Guid CartId { get; private set; }

    public Guid CustomerId { get; private set; }

    public string CustomerName { get; private set; } = string.Empty;

    /// <summary>E.164, e.g. +8801712345678.</summary>
    public string CustomerPhone { get; private set; } = string.Empty;

    public DeliveryAddress DeliveryAddress { get; private set; } = null!;

    /// <summary>The shopper's delivery instructions.</summary>
    public string? Note { get; private set; }

    public PaymentMethod PaymentMethod { get; private set; }

    public OrderStatus Status { get; private set; }

    /// <summary>The delivery zone chosen at checkout; null when the store had no delivery zones.</summary>
    public Guid? DeliveryZoneId { get; private set; }

    public string? DeliveryZoneName { get; private set; }

    /// <summary>Sum of the line totals.</summary>
    public decimal Subtotal { get; private set; }

    public decimal DeliveryFee { get; private set; }

    /// <summary>The store's VAT rate when the order was placed, in percent.</summary>
    public decimal VatRatePercent { get; private set; }

    public decimal Vat { get; private set; }

    /// <summary>Subtotal plus delivery fee plus VAT: what the shopper pays.</summary>
    public decimal Total { get; private set; }

    /// <summary>Sum of the payments Payments has reported for the order.</summary>
    public decimal AmountPaid { get; private set; }

    public OrderPaymentStatus PaymentStatus => PaymentStatusFor(Total, AmountPaid);

    /// <summary>The Inventory reservation holding this order's stock.</summary>
    public Guid ReservationId { get; private set; }

    public DateTimeOffset PlacedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Concurrency token (PostgreSQL xmin).</summary>
    public uint RowVersion { get; private set; }

    public IReadOnlyCollection<OrderLine> Lines => lines;

    public IReadOnlyCollection<OrderStatusChange> History => history;

    /// <summary>Loaded only when recording a payment.</summary>
    public IReadOnlyCollection<OrderPayment> Payments => payments;

    /// <param name="id">Chosen before the order exists, because the stock reservation is made for it first.</param>
    public static Order PlaceCashOnDelivery(
        Guid id,
        long number,
        Guid cartId,
        OrderCustomer customer,
        DeliveryAddress deliveryAddress,
        string? note,
        IReadOnlyList<OrderLineSpec> lines,
        OrderDelivery? delivery,
        decimal vatRatePercent,
        Guid reservationId,
        DateTimeOffset now) =>
        Place(id, number, cartId, customer, deliveryAddress, note, lines, delivery, vatRatePercent, PaymentMethod.CashOnDelivery, reservationId, now);

    /// <summary>
    /// Places an order paid the given way: cash on delivery starts <see cref="OrderStatus.Placed"/>, waiting for staff
    /// to confirm it; a prepaid order starts <see cref="OrderStatus.PendingPayment"/> and is confirmed by its payment.
    /// </summary>
    /// <param name="id">Chosen before the order exists, because the stock reservation is made for it first.</param>
    public static Order Place(
        Guid id,
        long number,
        Guid cartId,
        OrderCustomer customer,
        DeliveryAddress deliveryAddress,
        string? note,
        IReadOnlyList<OrderLineSpec> lines,
        OrderDelivery? delivery,
        decimal vatRatePercent,
        PaymentMethod paymentMethod,
        Guid reservationId,
        DateTimeOffset now)
    {
        if (lines.Count == 0)
        {
            throw new ArgumentException("An order needs at least one line.", nameof(lines));
        }

        var order = new Order
        {
            Id = id,
            Number = number,
            CartId = cartId,
            CustomerId = customer.CustomerId,
            CustomerName = customer.Name,
            CustomerPhone = customer.Phone,
            DeliveryAddress = deliveryAddress,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            PaymentMethod = paymentMethod,
            DeliveryZoneId = delivery?.ZoneId,
            DeliveryZoneName = delivery?.ZoneName,
            Status = paymentMethod.IsPrepaid() ? OrderStatus.PendingPayment : OrderStatus.Placed,
            ReservationId = reservationId,
            PlacedAt = now,
            UpdatedAt = now,
        };

        order.lines.AddRange(lines.Select((spec, index) => OrderLine.Create(id, index + 1, spec)));
        var charges = OrderCharges.Calculate(order.lines.Sum(line => line.LineTotal), delivery, vatRatePercent);
        order.Subtotal = charges.Subtotal;
        order.DeliveryFee = charges.DeliveryFee;
        order.VatRatePercent = charges.VatRatePercent;
        order.Vat = charges.Vat;
        order.Total = charges.Total;
        order.history.Add(OrderStatusChange.Create(id, from: null, order.Status, now, userId: null, note: null));
        return order;
    }

    public static OrderPaymentStatus PaymentStatusFor(decimal total, decimal amountPaid) => amountPaid switch
    {
        <= 0m => OrderPaymentStatus.Unpaid,
        _ when amountPaid >= total => OrderPaymentStatus.Paid,
        _ => OrderPaymentStatus.PartiallyPaid,
    };

    public bool CanMoveTo(OrderStatus target) => OrderTransitions.IsAllowed(Status, target);

    /// <summary>
    /// Confirms the order. <paramref name="reservationId"/> is the Inventory reservation holding its stock now, which
    /// differs from the one made at checkout when that one expired and the stock was reserved again.
    /// </summary>
    public Result Confirm(Guid reservationId, Guid? userId, string? note, DateTimeOffset now)
    {
        var moved = MoveTo(OrderStatus.Confirmed, userId, note, now);
        if (moved.IsSuccess)
        {
            ReservationId = reservationId;
        }

        return moved;
    }

    /// <summary>
    /// Confirms a prepaid order once its payments cover the total; staff never confirm a prepaid order. Like
    /// <see cref="Confirm"/>, <paramref name="reservationId"/> is the reservation holding its stock now.
    /// </summary>
    public Result ConfirmPaid(Guid reservationId, DateTimeOffset now)
    {
        if (Status != OrderStatus.PendingPayment)
        {
            return OrderingErrors.InvalidTransition(Status, OrderStatus.Confirmed);
        }

        if (PaymentStatus != OrderPaymentStatus.Paid)
        {
            return OrderingErrors.AwaitingPayment;
        }

        Record(OrderStatus.Confirmed, userId: null, "Paid online", now);
        ReservationId = reservationId;
        return Result.Success();
    }

    /// <summary>
    /// Cancels a prepaid order whose payment failed (Payments confirmed it with the gateway). Only an order still
    /// waiting for its payment, with nothing paid, is cancelled this way; anything else is for staff.
    /// </summary>
    public Result CancelUnpaid(string reason, DateTimeOffset now)
    {
        if (Status != OrderStatus.PendingPayment)
        {
            return OrderingErrors.InvalidTransition(Status, OrderStatus.Cancelled);
        }

        if (AmountPaid > 0m)
        {
            return OrderingErrors.PartlyPaid;
        }

        Record(OrderStatus.Cancelled, userId: null, $"Payment failed: {reason}", now);
        return Result.Success();
    }

    /// <summary>Moves the order along its lifecycle (see <see cref="OrderTransitions"/>) and records the change.</summary>
    public Result MoveTo(OrderStatus target, Guid? userId, string? note, DateTimeOffset now)
    {
        if (!CanMoveTo(target))
        {
            return OrderingErrors.InvalidTransition(Status, target);
        }

        Record(target, userId, note, now);
        return Result.Success();
    }

    /// <summary>
    /// Adds a payment Payments reported. Returns false, changing nothing, when this payment was already recorded,
    /// e.g. its event was delivered twice. Needs <see cref="Payments"/> loaded.
    /// </summary>
    public bool RecordPayment(Guid paymentId, string method, decimal amount, DateTimeOffset paidAt, DateTimeOffset now)
    {
        if (payments.Any(payment => payment.Id == paymentId))
        {
            return false;
        }

        payments.Add(OrderPayment.Create(paymentId, Id, method, amount, paidAt));
        AmountPaid += amount;
        UpdatedAt = now;
        return true;
    }

    private void Record(OrderStatus target, Guid? userId, string? note, DateTimeOffset now)
    {
        history.Add(OrderStatusChange.Create(Id, Status, target, now, userId, string.IsNullOrWhiteSpace(note) ? null : note.Trim()));
        Status = target;
        UpdatedAt = now;
    }
}
