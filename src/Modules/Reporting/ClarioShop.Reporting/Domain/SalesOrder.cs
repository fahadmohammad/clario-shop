using ClarioShop.SharedKernel.Domain;

namespace ClarioShop.Reporting.Domain;

/// <summary>
/// Reporting's own copy of an order, built from Ordering's events: what it was worth, what was on it, its latest
/// status and the store day it was sold on. An event may arrive twice, or after a later one, so a status change is
/// applied only when it is newer than the one the order shows.
/// </summary>
/// <remarks>
/// An order is a sale from the day it was confirmed (its stock left the shelf) for as long as it is not cancelled or
/// returned, so the sales reports are net of both.
/// </remarks>
internal sealed class SalesOrder : Entity
{
    private readonly List<SalesOrderLine> lines = [];

    private SalesOrder()
    {
    }

    /// <summary>E.g. <c>CS-000123</c>.</summary>
    public string Number { get; private set; } = string.Empty;

    public SalesOrderStatus Status { get; private set; }

    /// <summary>When <see cref="Status"/> was reached.</summary>
    public DateTimeOffset StatusChangedAt { get; private set; }

    /// <summary>E.g. <c>CashOnDelivery</c>; null until the order's placement is known.</summary>
    public string? PaymentMethod { get; private set; }

    public decimal Subtotal { get; private set; }

    public decimal DeliveryFee { get; private set; }

    public decimal Vat { get; private set; }

    public decimal Total { get; private set; }

    public DateTimeOffset? PlacedAt { get; private set; }

    /// <summary>The store's calendar day the order was placed on.</summary>
    public DateOnly? PlacedOn { get; private set; }

    public DateTimeOffset? ConfirmedAt { get; private set; }

    /// <summary>The store's calendar day the order was confirmed on, which is the day it counts as sold.</summary>
    public DateOnly? SoldOn { get; private set; }

    public IReadOnlyList<SalesOrderLine> Lines => lines;

    public uint RowVersion { get; private set; }

    /// <summary>Whether the order counts in the sales reports.</summary>
    public bool IsSale => SoldOn is not null && Status is not (SalesOrderStatus.Cancelled or SalesOrderStatus.Returned);

    /// <summary>The order as first heard of, from whichever of its events came first.</summary>
    public static SalesOrder Start(Guid orderId, string number, SalesOrderStatus status, DateTimeOffset at) => new()
    {
        Id = orderId,
        Number = number,
        Status = status,
        StatusChangedAt = at,
    };

    /// <summary>Records what the order was worth and what was on it; once only.</summary>
    public void RecordPlaced(SalesOrderPlacement placement)
    {
        if (PlacedAt is not null)
        {
            return;
        }

        PaymentMethod = placement.PaymentMethod;
        Subtotal = placement.Subtotal;
        DeliveryFee = placement.DeliveryFee;
        Vat = placement.Vat;
        Total = placement.Total;
        PlacedAt = placement.PlacedAt;
        PlacedOn = placement.PlacedOn;
        for (var index = 0; index < placement.Lines.Count; index++)
        {
            lines.Add(SalesOrderLine.Create(Id, index + 1, placement.Lines[index]));
        }

        MoveTo(placement.Status, placement.PlacedAt);
    }

    /// <summary>Records the sale: the first confirmation decides the day it counts on.</summary>
    /// <param name="soldOn">The store's calendar day of <paramref name="at"/>.</param>
    public void RecordConfirmed(DateTimeOffset at, DateOnly soldOn)
    {
        if (ConfirmedAt is null)
        {
            ConfirmedAt = at;
            SoldOn = soldOn;
        }

        MoveTo(SalesOrderStatus.Confirmed, at);
    }

    /// <summary>Takes the status when it is newer than the current one; returns whether it did.</summary>
    public bool MoveTo(SalesOrderStatus status, DateTimeOffset at)
    {
        var isNewer = at > StatusChangedAt || (at == StatusChangedAt && status > Status);
        if (!isNewer)
        {
            return false;
        }

        Status = status;
        StatusChangedAt = at;
        return true;
    }
}
