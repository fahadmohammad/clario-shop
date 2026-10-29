using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace ClarioShop.Infrastructure.Auditing;

/// <summary>
/// Adds audit rows to a module DbContext, so they are saved in the same transaction as the change they describe.
/// The user and device come from the current request.
/// </summary>
public sealed class AuditTrail(IHttpContextAccessor httpContextAccessor, TimeProvider clock)
{
    public const string DeviceHeader = "X-Device-Id";

    public const int DeviceMaxLength = 200;

    public void Record(DbContext db, string action, string entityType, Guid entityId, object? before, object? after)
    {
        var context = httpContextAccessor.HttpContext;
        db.Set<AuditLogEntry>().Add(new AuditLogEntry
        {
            Id = Guid.CreateVersion7(),
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Before = Serialize(before),
            After = Serialize(after),
            UserId = Guid.TryParse(context?.User.FindFirst("sub")?.Value, out var userId) ? userId : null,
            Device = DeviceOf(context),
            OccurredAt = clock.GetUtcNow(),
        });
    }

    private static string? Serialize(object? values) =>
        values is null ? null : JsonSerializer.Serialize(values, values.GetType(), JsonSerializerOptions.Web);

    private static string? DeviceOf(HttpContext? context)
    {
        if (context is null)
        {
            return null;
        }

        var device = context.Request.Headers[DeviceHeader].ToString();
        if (string.IsNullOrWhiteSpace(device))
        {
            device = context.Request.Headers.UserAgent.ToString();
        }

        device = device.Trim();
        return device.Length == 0 ? null : device[..Math.Min(device.Length, DeviceMaxLength)];
    }
}
