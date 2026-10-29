using System.Text.Json.Serialization;
using ClarioShop.Inventory.Domain;
using ClarioShop.Inventory.Persistence;
using ClarioShop.Inventory.Services;

namespace ClarioShop.Inventory.Features.Transfers;

/// <summary>Moves stock from one location to another in one document, both sides saved together.</summary>
public static class RecordStockTransfer
{
    public sealed record Command(Guid FromLocationId, Guid ToLocationId, string? Note, IReadOnlyList<Line> Lines)
    {
        /// <summary>Set by the endpoint from the access token.</summary>
        [JsonIgnore]
        public Guid? UserId { get; init; }
    }

    public sealed record Line(Guid VariantId, decimal Quantity);

    /// <param name="From">Stock left at the source after the transfer.</param>
    /// <param name="To">Stock at the destination after the transfer.</param>
    public sealed record Response(Guid Id, Guid FromLocationId, Guid ToLocationId, IReadOnlyList<StockLevelItem> From, IReadOnlyList<StockLevelItem> To);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.FromLocationId).NotEmpty();
            RuleFor(command => command.ToLocationId).NotEmpty()
                .NotEqual(command => command.FromLocationId).WithMessage("Stock can only be transferred to a different location.");
            RuleFor(command => command.Note).MaximumLength(1000);
            RuleFor(command => command.Lines).NotEmpty();
            RuleFor(command => command.Lines).Must(lines => lines.Select(l => l.VariantId).Distinct().Count() == lines.Count)
                .When(command => command.Lines is not null)
                .WithMessage("Each variant may appear on one line only.");
            RuleForEach(command => command.Lines).ChildRules(line =>
            {
                line.RuleFor(l => l.VariantId).NotEmpty();
                line.RuleFor(l => l.Quantity).GreaterThan(0).PrecisionScale(18, 3, ignoreTrailingZeros: true);
            });
        }
    }

    internal sealed class Handler(InventoryDbContext db, ItemRegistry items, TimeProvider clock) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> HandleAsync(Command command, CancellationToken cancellationToken) =>
            await ConcurrencyRetry.ExecuteAsync<Response>(db, async ct =>
            {
                Guid[] locationIds = [command.FromLocationId, command.ToLocationId];
                var active = await db.Locations
                    .Where(location => locationIds.Contains(location.Id) && location.IsActive)
                    .Select(location => location.Id)
                    .ToListAsync(ct);
                if (!active.Contains(command.FromLocationId))
                {
                    return Error.Validation("fromLocationId", "The location does not exist or is inactive.");
                }

                if (!active.Contains(command.ToLocationId))
                {
                    return Error.Validation("toLocationId", "The location does not exist or is inactive.");
                }

                var variantIds = command.Lines.Select(l => l.VariantId).ToList();
                var known = await items.EnsureKnownAsync(variantIds, ct);
                if (known.IsFailure)
                {
                    return known.Error;
                }

                var now = clock.GetUtcNow();
                var created = StockTransfer.Create(
                    command.FromLocationId,
                    command.ToLocationId,
                    command.Note,
                    command.Lines.Select(l => (l.VariantId, l.Quantity)).ToList(),
                    now,
                    command.UserId);
                if (created.IsFailure)
                {
                    return created.Error;
                }

                var transfer = created.Value;
                var sources = await db.LoadOrCreateAsync(transfer.FromLocationId, variantIds, now, ct);
                var destinations = await db.LoadOrCreateAsync(transfer.ToLocationId, variantIds, now, ct);
                foreach (var (sent, received) in transfer.ToMovements())
                {
                    var applied = sources[sent.VariantId].ApplyTransferOut(sent);
                    if (applied.IsFailure)
                    {
                        return applied.Error;
                    }

                    destinations[received.VariantId].Apply(received);
                    db.Movements.AddRange(sent, received);
                }

                db.Transfers.Add(transfer);
                await db.SaveChangesAsync(ct);
                return new Response(
                    transfer.Id,
                    transfer.FromLocationId,
                    transfer.ToLocationId,
                    StockLevelItem.From(sources.Values),
                    StockLevelItem.From(destinations.Values));
            }, cancellationToken);
    }
}
