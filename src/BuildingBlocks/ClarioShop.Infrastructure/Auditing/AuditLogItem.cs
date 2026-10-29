using System.Text.Json;

namespace ClarioShop.Infrastructure.Auditing;

public sealed record AuditLogItem(
    Guid Id,
    string Action,
    string EntityType,
    Guid EntityId,
    JsonElement? Before,
    JsonElement? After,
    Guid? UserId,
    string? Device,
    DateTimeOffset OccurredAt);
