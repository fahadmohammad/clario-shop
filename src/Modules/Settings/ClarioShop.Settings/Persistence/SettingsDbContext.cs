using ClarioShop.Infrastructure.Persistence;
using ClarioShop.Settings.Domain;

namespace ClarioShop.Settings.Persistence;

internal sealed class SettingsDbContext(DbContextOptions<SettingsDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "settings";

    public override string Schema => SchemaName;

    public DbSet<StoreSettings> StoreSettings => Set<StoreSettings>();

    public DbSet<DeliveryZone> DeliveryZones => Set<DeliveryZone>();

    public DbSet<PaymentGatewayAccount> PaymentGatewayAccounts => Set<PaymentGatewayAccount>();

    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
}
