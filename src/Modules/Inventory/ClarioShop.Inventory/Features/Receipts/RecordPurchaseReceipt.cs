using System.Text.Json.Serialization;
using ClarioShop.Inventory.Domain;
using ClarioShop.Inventory.Persistence;
using ClarioShop.Inventory.Services;

namespace ClarioShop.Inventory.Features.Receipts;

public static class RecordPurchaseReceipt
{
    /// <param name="LocationId">Where the goods arrived; the default location when null.</param>
    /// <param name="ReceivedAt">When the goods arrived; now when null.</param>
    public sealed record Command(
        Guid? LocationId,
        string? SupplierName,
        string? SupplierReference,
        DateTimeOffset? ReceivedAt,
        string? Note,
        IReadOnlyList<Line> Lines)
    {
        /// <summary>Set by the endpoint from the access token.</summary>
        [JsonIgnore]
        public Guid? UserId { get; init; }
    }

    public sealed record Line(Guid VariantId, decimal Quantity, decimal UnitCost);

    public sealed record Response(Guid Id, Guid LocationId, decimal TotalCost, IReadOnlyList<StockLevelItem> Levels);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.SupplierName).MaximumLength(200);
            RuleFor(command => command.SupplierReference).MaximumLength(100);
            RuleFor(command => command.Note).MaximumLength(1000);
            RuleFor(command => command.Lines).NotEmpty();
            RuleFor(command => command.Lines).Must(lines => lines.Select(l => l.VariantId).Distinct().Count() == lines.Count)
                .When(command => command.Lines is not null)
                .WithMessage("Each variant may appear on one line only.");
            RuleForEach(command => command.Lines).ChildRules(line =>
            {
                line.RuleFor(l => l.VariantId).NotEmpty();
                line.RuleFor(l => l.Quantity).GreaterThan(0).PrecisionScale(18, 3, ignoreTrailingZeros: true);
                line.RuleFor(l => l.UnitCost).GreaterThanOrEqualTo(0).PrecisionScale(18, 2, ignoreTrailingZeros: true);
            });
        }
    }

    internal sealed class Handler(InventoryDbContext db, LocationResolver locations, ItemRegistry items, TimeProvider clock)
        : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> HandleAsync(Command command, CancellationToken cancellationToken) =>
            await ConcurrencyRetry.ExecuteAsync<Response>(db, async ct =>
            {
                var location = await locations.ResolveAsync(command.LocationId, ct);
                if (location.IsFailure)
                {
                    return location.Error;
                }

                var variantIds = command.Lines.Select(l => l.VariantId).ToList();
                var known = await items.EnsureKnownAsync(variantIds, ct);
                if (known.IsFailure)
                {
                    return known.Error;
                }

                var now = clock.GetUtcNow();
                var created = PurchaseReceipt.Create(
                    location.Value,
                    command.SupplierName,
                    command.SupplierReference,
                    command.Note,
                    command.Lines.Select(l => (l.VariantId, l.Quantity, l.UnitCost)).ToList(),
                    command.ReceivedAt ?? now,
                    now,
                    command.UserId);
                if (created.IsFailure)
                {
                    return created.Error;
                }

                var receipt = created.Value;
                var levels = await db.LoadOrCreateAsync(location.Value, variantIds, now, ct);
                foreach (var movement in receipt.ToMovements())
                {
                    db.Movements.Add(movement);
                    levels[movement.VariantId].Apply(movement);
                }

                db.PurchaseReceipts.Add(receipt);
                await db.SaveChangesAsync(ct);
                return new Response(receipt.Id, receipt.LocationId, receipt.TotalCost, StockLevelItem.From(levels.Values));
            }, cancellationToken);
    }
}
