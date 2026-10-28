using ClarioShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Design;

namespace ClarioShop.Notifications.Persistence;

internal sealed class NotificationsDesignTimeFactory : IDesignTimeDbContextFactory<NotificationsDbContext>
{
    public NotificationsDbContext CreateDbContext(string[] args) =>
        new(TenantDbContextRegistration.DesignTimeOptions<NotificationsDbContext>(NotificationsDbContext.SchemaName));
}
