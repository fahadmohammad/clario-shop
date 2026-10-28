using ClarioShop.Infrastructure.Handlers;
using ClarioShop.Infrastructure.Messaging;
using ClarioShop.Infrastructure.Persistence;
using ClarioShop.Ordering.Contracts;
using ClarioShop.Ordering.Features.Payments;
using ClarioShop.Ordering.Persistence;
using ClarioShop.Ordering.Services;
using ClarioShop.Payments.Contracts.Events;
using ClarioShop.SharedKernel.Security;

namespace ClarioShop.Ordering;

public static class OrderingModule
{
    public static IServiceCollection AddOrderingModule(this IServiceCollection services)
    {
        services.AddTenantDbContext<OrderingDbContext>(OrderingDbContext.SchemaName);
        services.AddModuleHandlers(typeof(OrderingModule).Assembly);
        services.AddEndpointGroups(typeof(OrderingModule).Assembly);

        services.AddSingleton<IPermissionProvider, OrderingPermissionProvider>();
        services.AddScoped<IOrderQueries, OrderQueries>();

        services.AddIntegrationEventHandler<PaymentSucceeded, PaymentSucceededHandler, OrderingDbContext>("ordering");
        services.AddIntegrationEventHandler<PaymentFailed, PaymentFailedHandler, OrderingDbContext>("ordering");
        return services;
    }
}
