namespace ClarioShop.Infrastructure.Auditing;

/// <summary>
/// One sensitive action: who did what to which record, the values before and after, when and from which device.
/// Each module that audits keeps its own <c>AuditLog</c> table in its schema; rows are never changed or deleted.
/// </summary>
public sealed class AuditLogEntry
{
    public Guid Id { get; init; }

    /// <summary>What happened, as <c>module.entity.verb</c> (e.g. <c>inventory.stock.adjusted</c>).</summary>
    public string Action { get; init; } = string.Empty;

    public string EntityType { get; init; } = string.Empty;

    public Guid EntityId { get; init; }

    /// <summary>The audited values before the change, as JSON; null when the record was created.</summary>
    public string? Before { get; init; }

    /// <summary>The audited values after the change, as JSON.</summary>
    public string? After { get; init; }

    /// <summary>The staff member who did it; null for background jobs.</summary>
    public Guid? UserId { get; init; }

    /// <summary>The device id the client sent (<c>X-Device-Id</c>, e.g. a POS till), or its user agent.</summary>
    public string? Device { get; init; }

    public DateTimeOffset OccurredAt { get; init; }
}
