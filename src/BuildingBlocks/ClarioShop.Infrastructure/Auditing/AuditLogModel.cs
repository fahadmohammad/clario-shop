using Microsoft.EntityFrameworkCore;

namespace ClarioShop.Infrastructure.Auditing;

public static class AuditLogModel
{
    /// <summary>Adds the module's own <c>AuditLog</c> table. Call from <c>OnModelCreating</c> of a module that audits.</summary>
    public static ModelBuilder MapAuditLog(this ModelBuilder modelBuilder, string schema)
    {
        modelBuilder.Entity<AuditLogEntry>(entry =>
        {
            entry.ToTable("AuditLog");
            entry.HasKey(e => e.Id);
            entry.Property(e => e.Action).HasMaxLength(100);
            entry.Property(e => e.EntityType).HasMaxLength(100);
            entry.Property(e => e.Before).HasColumnType("jsonb");
            entry.Property(e => e.After).HasColumnType("jsonb");
            entry.Property(e => e.Device).HasMaxLength(AuditTrail.DeviceMaxLength);
            entry.HasIndex(e => e.OccurredAt).HasDatabaseName($"IX_{schema}_AuditLog_OccurredAt");
            entry.HasIndex(e => new { e.EntityType, e.EntityId }).HasDatabaseName($"IX_{schema}_AuditLog_Entity");
        });

        return modelBuilder;
    }
}
