using ClarioShop.Inventory.Services;

namespace ClarioShop.Inventory.Features.Drift;

/// <summary>Runs the drift check now, the same check the nightly job runs.</summary>
public static class RunDriftCheck
{
    public sealed record Command;

    public sealed record Response(Guid CheckId, int CheckedLevels, IReadOnlyList<ListStockDrifts.DriftItem> Drifts);

    internal sealed class Handler(StockDriftCheck check) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            var outcome = await check.RunAsync(cancellationToken);
            if (outcome.IsFailure)
            {
                return outcome.Error;
            }

            var (checkId, checkedLevels, drifts) = outcome.Value;
            return new Response(checkId, checkedLevels, drifts.Select(ListStockDrifts.DriftItem.From).ToList());
        }
    }
}
