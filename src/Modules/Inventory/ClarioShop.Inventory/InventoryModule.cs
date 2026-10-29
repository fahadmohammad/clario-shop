using ClarioShop.Catalog.Contracts.Events;
using ClarioShop.Infrastructure.Handlers;
using ClarioShop.Infrastructure.Messaging;
using ClarioShop.Infrastructure.Persistence;
using ClarioShop.Infrastructure.Tenancy;
using ClarioShop.Inventory.Contracts;
using ClarioShop.Inventory.Features.Items;
using ClarioShop.Inventory.Features.Orders;
using ClarioShop.Inventory.Persistence;
using ClarioShop.Inventory.Services;
using ClarioShop.Ordering.Contracts.Events;
using ClarioShop.SharedKernel.Security;
using Microsoft.Extensions.Configuration;

namespace ClarioShop.Inventory;

public static class InventoryModule
{
    public static IServiceCollection AddInventoryModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<InventoryOptions>(configuration.GetSection(InventoryOptions.SectionName));
        services.AddTenantDbContext<InventoryDbContext>(InventoryDbContext.SchemaName);
        services.AddModuleHandlers(typeof(InventoryModule).Assembly);
        services.AddEndpointGroups(typeof(InventoryModule).Assembly);

        services.AddSingleton<IPermissionProvider, InventoryPermissionProvider>();
        services.AddScoped<LocationResolver>();
        services.AddScoped<ItemRegistry>();
        services.AddScoped<ReservationExpiry>();
        services.AddScoped<InventoryReservations>();
        services.AddScoped<IInventoryReservations>(provider => provider.GetRequiredService<InventoryReservations>());
        services.AddScoped<IInventoryQueries, InventoryQueries>();
        services.AddScoped<StockDriftCheck>();
        services.AddScoped<ITenantInitializer, DefaultLocationInitializer>();
        services.AddHostedService<ReservationExpiryService>();
        services.AddHostedService<StockDriftCheckService>();

        services.AddIntegrationEventHandler<ProductCreated, ProductCreatedHandler, InventoryDbContext>("inventory");
        services.AddIntegrationEventHandler<OrderConfirmed, OrderConfirmedHandler, InventoryDbContext>("inventory");
        services.AddIntegrationEventHandler<OrderCancelled, OrderCancelledHandler, InventoryDbContext>("inventory");
        return services;
    }
}
