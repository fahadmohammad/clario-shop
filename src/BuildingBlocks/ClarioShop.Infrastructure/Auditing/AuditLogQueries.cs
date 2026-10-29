using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace ClarioShop.Infrastructure.Auditing;

public static class AuditLogQueries
{
    /// <summary>A page of a module's audit log, newest first.</summary>
    public static async Task<AuditLogPage> ListAsync(this DbContext db, AuditLogFilter filter, CancellationToken cancellationToken)
    {
        var entries = db.Set<AuditLogEntry>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            entries = entries.Where(entry => entry.Action == filter.Action);
        }

        if (filter.EntityId is { } entityId)
        {
            entries = entries.Where(entry => entry.EntityId == entityId);
        }

        if (filter.UserId is { } userId)
        {
            entries = entries.Where(entry => entry.UserId == userId);
        }

        if (filter.From is { } from)
        {
            entries = entries.Where(entry => entry.OccurredAt >= from);
        }

        if (filter.To is { } to)
        {
            entries = entries.Where(entry => entry.OccurredAt < to);
        }

        var total = await entries.CountAsync(cancellationToken);
        var page = await entries
            .OrderByDescending(entry => entry.OccurredAt)
            .ThenByDescending(entry => entry.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        var items = page.Select(entry => new AuditLogItem(
                entry.Id,
                entry.Action,
                entry.EntityType,
                entry.EntityId,
                Parse(entry.Before),
                Parse(entry.After),
                entry.UserId,
                entry.Device,
                entry.OccurredAt))
            .ToList();
        return new AuditLogPage(items, filter.Page, filter.PageSize, total);
    }

    private static JsonElement? Parse(string? json)
    {
        if (json is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
