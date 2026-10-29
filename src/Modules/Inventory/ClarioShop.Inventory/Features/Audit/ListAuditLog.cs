using ClarioShop.Infrastructure.Auditing;
using ClarioShop.Inventory.Persistence;

namespace ClarioShop.Inventory.Features.Audit;

/// <summary>Inventory's audit log (stock adjustments), newest first.</summary>
public static class ListAuditLog
{
    /// <param name="Action">Exact action, e.g. <c>inventory.stock.adjusted</c>.</param>
    /// <param name="EntityId">Rows about this record, e.g. one adjustment.</param>
    /// <param name="UserId">Rows recorded for this staff member.</param>
    /// <param name="From">On or after this instant.</param>
    /// <param name="To">Before this instant.</param>
    public sealed record Query(
        string? Action,
        Guid? EntityId,
        Guid? UserId,
        DateTimeOffset? From,
        DateTimeOffset? To,
        int Page = 1,
        int PageSize = 50);

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
            RuleFor(query => query.PageSize).InclusiveBetween(1, 200);
            RuleFor(query => query.To).GreaterThan(query => query.From)
                .When(query => query.From is not null && query.To is not null);
        }
    }

    internal sealed class Handler(InventoryDbContext db) : ICommandHandler<Query, AuditLogPage>
    {
        public async Task<Result<AuditLogPage>> HandleAsync(Query query, CancellationToken cancellationToken) =>
            await db.ListAsync(
                new AuditLogFilter(query.Action, query.EntityId, query.UserId, query.From, query.To, query.Page, query.PageSize),
                cancellationToken);
    }
}
