using ClarioShop.Infrastructure.Handlers;
using ClarioShop.Infrastructure.Messaging;
using ClarioShop.Infrastructure.Persistence;
using ClarioShop.Inventory.Contracts.Events;
using ClarioShop.Notifications.Contracts;
using ClarioShop.Notifications.Features.Orders;
using ClarioShop.Notifications.Features.Retries;
using ClarioShop.Notifications.Features.StockAlerts;
using ClarioShop.Notifications.Persistence;
using ClarioShop.Notifications.Services;
using ClarioShop.Ordering.Contracts.Events;
using ClarioShop.SharedKernel.Security;
using Microsoft.Extensions.Configuration;

namespace ClarioShop.Notifications;

public static class NotificationsModule
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddTenantDbContext<NotificationsDbContext>(NotificationsDbContext.SchemaName);
        services.AddModuleHandlers(typeof(NotificationsModule).Assembly);
        services.AddEndpointGroups(typeof(NotificationsModule).Assembly);

        services.AddSingleton<IPermissionProvider, NotificationsPermissionProvider>();
        services.Configure<NotificationOptions>(configuration.GetSection(NotificationOptions.SectionName));
        services.AddScoped<NotificationSender>();
        services.AddScoped<OrderNotifier>();
        services.AddScoped<StaffAlerter>();
        services.AddSingleton<INotificationTemplateDefaults, NotificationTemplateDefaults>();
        services.AddScoped<NotificationRetry>();
        services.AddHostedService<NotificationRetryService>();

        services.AddIntegrationEventHandler<OrderPlaced, OrderPlacedHandler, NotificationsDbContext>("notifications");
        services.AddIntegrationEventHandler<OrderConfirmed, OrderConfirmedHandler, NotificationsDbContext>("notifications");
        services.AddIntegrationEventHandler<OrderShipped, OrderShippedHandler, NotificationsDbContext>("notifications");
        services.AddIntegrationEventHandler<OrderDelivered, OrderDeliveredHandler, NotificationsDbContext>("notifications");
        services.AddIntegrationEventHandler<StockWentNegative, StockWentNegativeHandler, NotificationsDbContext>("notifications");
        services.AddIntegrationEventHandler<StockDriftDetected, StockDriftDetectedHandler, NotificationsDbContext>("notifications");
        return services;
    }
}
