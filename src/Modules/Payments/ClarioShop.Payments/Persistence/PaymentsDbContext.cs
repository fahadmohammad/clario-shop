using ClarioShop.Infrastructure.Persistence;
using ClarioShop.Payments.Domain;

namespace ClarioShop.Payments.Persistence;

internal sealed class PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "payments";

    /// <summary>First key of the advisory locks taken per order, so they cannot collide with another module's locks.</summary>
    private const int OrderLockClass = 0x50415953; // "PAYS"

    public override string Schema => SchemaName;

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<GatewayWebhookLog> GatewayWebhookLogs => Set<GatewayWebhookLog>();

    /// <summary>
    /// Serializes recording payments for one order until the current transaction ends, so two collections at once
    /// cannot both pass the outstanding-amount check, and two gateway callbacks cannot both settle one payment.
    /// </summary>
    public async Task LockOrderAsync(Guid orderId, CancellationToken cancellationToken) =>
        await Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({OrderLockClass}, hashtext({orderId.ToString()}))", cancellationToken);
}
