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

    public decimal Subtotal { get; private set; }

    public decimal Total { get; private set; }

    /// <summary>The Inventory reservation holding this order's stock.</summary>
    public Guid ReservationId { get; private set; }

    public DateTimeOffset PlacedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Concurrency token (PostgreSQL xmin).</summary>
    public uint RowVersion { get; private set; }

    public IReadOnlyCollection<OrderLine> Lines => lines;

    public IReadOnlyCollection<OrderStatusChange> History => history;

    /// <param name="id">Chosen before the order exists, because the stock reservation is made for it first.</param>
    public static Order PlaceCashOnDelivery(
        Guid id,
        long number,
        Guid cartId,
        OrderCustomer customer,
        DeliveryAddress deliveryAddress,
        string? note,
        IReadOnlyList<OrderLineSpec> lines,
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
            PaymentMethod = PaymentMethod.CashOnDelivery,
            Status = OrderStatus.Placed,
            ReservationId = reservationId,
            PlacedAt = now,
            UpdatedAt = now,
        };

        order.lines.AddRange(lines.Select((spec, index) => OrderLine.Create(id, index + 1, spec)));
        order.Subtotal = order.lines.Sum(line => line.LineTotal);
        order.Total = order.Subtotal;
        order.history.Add(OrderStatusChange.Create(id, from: null, OrderStatus.Placed, now, userId: null, note: null));
        return order;
    }

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

    /// <summary>Moves the order along its lifecycle (see <see cref="OrderTransitions"/>) and records the change.</summary>
    public Result MoveTo(OrderStatus target, Guid? userId, string? note, DateTimeOffset now)
    {
        if (!CanMoveTo(target))
        {
            return OrderingErrors.InvalidTransition(Status, target);
        }

        history.Add(OrderStatusChange.Create(Id, Status, target, now, userId, string.IsNullOrWhiteSpace(note) ? null : note.Trim()));
        Status = target;
        UpdatedAt = now;
        return Result.Success();
    }
}
