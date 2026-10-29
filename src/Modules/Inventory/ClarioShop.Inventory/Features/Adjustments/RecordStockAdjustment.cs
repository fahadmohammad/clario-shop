using System.Text.Json.Serialization;
using ClarioShop.Infrastructure.Auditing;
using ClarioShop.Inventory.Domain;
using ClarioShop.Inventory.Persistence;
using ClarioShop.Inventory.Services;

namespace ClarioShop.Inventory.Features.Adjustments;

public static class RecordStockAdjustment
{
    /// <param name="Reason">Damaged, Expired, Theft, CountCorrection or Other.</param>
    /// <param name="LocationId">Where the stock is; the default location when null.</param>
    public sealed record Command(Guid? LocationId, string Reason, string? Note, IReadOnlyList<Line> Lines)
    {
        /// <summary>Set by the endpoint from the access token.</summary>
        [JsonIgnore]
        public Guid? UserId { get; init; }
    }

    /// <param name="QuantityChange">Signed: negative removes stock, positive adds it.</param>
    public sealed record Line(Guid VariantId, decimal QuantityChange);

    public sealed record Response(Guid Id, Guid LocationId, string Reason, IReadOnlyList<StockLevelItem> Levels);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.Reason).NotEmpty().IsEnumName(typeof(AdjustmentReason), caseSensitive: false)
                .WithMessage($"Reason must be one of: {string.Join(", ", Enum.GetNames<AdjustmentReason>())}.");
            RuleFor(command => command.Note).MaximumLength(1000);
            RuleFor(command => command.Lines).NotEmpty();
            RuleFor(command => command.Lines).Must(lines => lines.Select(l => l.VariantId).Distinct().Count() == lines.Count)
                .When(command => command.Lines is not null)
                .WithMessage("Each variant may appear on one line only.");
            RuleForEach(command => command.Lines).ChildRules(line =>
            {
                line.RuleFor(l => l.VariantId).NotEmpty();
                line.RuleFor(l => l.QuantityChange).NotEqual(0).PrecisionScale(18, 3, ignoreTrailingZeros: true);
            });
        }
    }

    /// <summary>The audited values of an adjustment: each adjusted variant's stock on hand at the location.</summary>
    internal sealed record AuditedLevel(Guid VariantId, decimal OnHand);

    internal sealed class Handler(
        InventoryDbContext db,
        LocationResolver locations,
        ItemRegistry items,
        AuditTrail audit,
        TimeProvider clock)
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
                var created = StockAdjustment.Create(
                    location.Value,
                    Enum.Parse<AdjustmentReason>(command.Reason, ignoreCase: true),
                    command.Note,
                    command.Lines.Select(l => (l.VariantId, l.QuantityChange)).ToList(),
                    now,
                    command.UserId);
                if (created.IsFailure)
                {
                    return created.Error;
                }

                var adjustment = created.Value;
                var levels = await db.LoadOrCreateAsync(location.Value, variantIds, now, ct);
                var before = Audited(levels.Values);
                foreach (var movement in adjustment.ToMovements())
                {
                    var applied = levels[movement.VariantId].ApplyAdjustment(movement);
                    if (applied.IsFailure)
                    {
                        return applied.Error;
                    }

                    db.Movements.Add(movement);
                }

                db.Adjustments.Add(adjustment);
                audit.Record(
                    db,
                    AuditActions.StockAdjusted,
                    nameof(StockAdjustment),
                    adjustment.Id,
                    new { adjustment.LocationId, Levels = before },
                    new { adjustment.LocationId, Reason = adjustment.Reason.ToString(), adjustment.Note, Levels = Audited(levels.Values) });
                await db.SaveChangesAsync(ct);
                return new Response(adjustment.Id, adjustment.LocationId, adjustment.Reason.ToString(), StockLevelItem.From(levels.Values));
            }, cancellationToken);

        private static List<AuditedLevel> Audited(IEnumerable<StockLevel> levels) =>
            levels.OrderBy(level => level.VariantId).Select(level => new AuditedLevel(level.VariantId, level.OnHand)).ToList();
    }
}
