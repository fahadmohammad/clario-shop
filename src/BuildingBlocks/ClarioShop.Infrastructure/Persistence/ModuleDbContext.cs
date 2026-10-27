using System.Text.Json;
using ClarioShop.Infrastructure.Messaging;
using ClarioShop.SharedKernel.Messaging;
using Microsoft.EntityFrameworkCore;

namespace ClarioShop.Infrastructure.Persistence;

/// <summary>
/// Base for every module DbContext: own schema, outbox and inbox tables, and integration events
/// written to the outbox atomically with the module's changes.
/// </summary>
public abstract class ModuleDbContext(DbContextOptions options) : DbContext(options)
{
    private readonly List<IIntegrationEvent> pendingEvents = [];

    public abstract string Schema { get; }

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    /// <summary>Queues an integration event; it is stored in the outbox by the next SaveChanges.</summary>
    public void Publish(IIntegrationEvent integrationEvent) => pendingEvents.Add(integrationEvent);

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        foreach (var integrationEvent in pendingEvents)
        {
            OutboxMessages.Add(new OutboxMessage
            {
                Id = integrationEvent.EventId,
                Type = IntegrationEventNames.For(integrationEvent.GetType()),
                Content = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), JsonSerializerOptions.Web),
                OccurredAt = integrationEvent.OccurredAt,
            });
        }

        pendingEvents.Clear();
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        throw new NotSupportedException("Use SaveChangesAsync so integration events reach the outbox.");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<OutboxMessage>(outbox =>
        {
            outbox.ToTable("OutboxMessages");
            outbox.HasKey(message => message.Id);
            outbox.Property(message => message.Type).HasMaxLength(300);
            outbox.Property(message => message.Content).HasColumnType("jsonb");
            outbox.Property(message => message.Error).HasMaxLength(2000);
            outbox.HasIndex(message => message.OccurredAt)
                .HasFilter("\"ProcessedAt\" IS NULL")
                .HasDatabaseName($"IX_{Schema}_OutboxMessages_Unprocessed");
        });

        modelBuilder.Entity<InboxMessage>(inbox =>
        {
            inbox.ToTable("InboxMessages");
            inbox.HasKey(message => new { message.MessageId, message.Handler });
            inbox.Property(message => message.Handler).HasMaxLength(300);
        });

        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
    }
}
