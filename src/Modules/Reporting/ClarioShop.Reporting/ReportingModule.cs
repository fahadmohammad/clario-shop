using ClarioShop.Infrastructure.Handlers;
using ClarioShop.Infrastructure.Messaging;
using ClarioShop.Infrastructure.Persistence;
using ClarioShop.Ordering.Contracts.Events;
using ClarioShop.Reporting.Features.Projections;
using ClarioShop.Reporting.Persistence;
using ClarioShop.Reporting.Services;
using ClarioShop.SharedKernel.Security;
using Microsoft.Extensions.Configuration;

namespace ClarioShop.Reporting;

public static class ReportingModule
{
    public static IServiceCollection AddReportingModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddTenantDbContext<ReportingDbContext>(ReportingDbContext.SchemaName);
        services.AddModuleHandlers(typeof(ReportingModule).Assembly);
        services.AddEndpointGroups(typeof(ReportingModule).Assembly);

        services.AddSingleton<IPermissionProvider, ReportingPermissionProvider>();
        var options = configuration.GetSection(ReportingOptions.SectionName).Get<ReportingOptions>() ?? new ReportingOptions();
        services.AddSingleton(options.Calendar());
        services.AddScoped<SalesOrderProjection>();

        services.AddIntegrationEventHandler<OrderPlaced, OrderPlacedHandler, ReportingDbContext>("reporting");
        services.AddIntegrationEventHandler<OrderConfirmed, OrderConfirmedHandler, ReportingDbContext>("reporting");
        services.AddIntegrationEventHandler<OrderPacked, OrderPackedHandler, ReportingDbContext>("reporting");
        services.AddIntegrationEventHandler<OrderShipped, OrderShippedHandler, ReportingDbContext>("reporting");
        services.AddIntegrationEventHandler<OrderDelivered, OrderDeliveredHandler, ReportingDbContext>("reporting");
        services.AddIntegrationEventHandler<OrderCancelled, OrderCancelledHandler, ReportingDbContext>("reporting");
        services.AddIntegrationEventHandler<OrderReturned, OrderReturnedHandler, ReportingDbContext>("reporting");
        return services;
    }
}
