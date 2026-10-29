namespace ClarioShop.Infrastructure.Auditing;

public sealed record AuditLogPage(IReadOnlyList<AuditLogItem> Items, int Page, int PageSize, int TotalCount);
