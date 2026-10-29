namespace ClarioShop.Infrastructure.Auditing;

/// <param name="Action">Exact action, e.g. <c>inventory.stock.adjusted</c>.</param>
/// <param name="EntityId">Rows about this record.</param>
/// <param name="UserId">Rows recorded for this staff member.</param>
/// <param name="From">On or after this instant.</param>
/// <param name="To">Before this instant.</param>
public sealed record AuditLogFilter(
    string? Action,
    Guid? EntityId,
    Guid? UserId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Page = 1,
    int PageSize = 50);
