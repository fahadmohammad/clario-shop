using ClarioShop.Infrastructure.Persistence;
using ClarioShop.Notifications.Domain;

namespace ClarioShop.Notifications.Persistence;

internal sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "notifications";

    public override string Schema => SchemaName;

    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
}
