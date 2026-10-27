using ClarioShop.Infrastructure.Persistence;
using ClarioShop.SharedKernel.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ClarioShop.Infrastructure.Messaging;

public static class MessagingRegistration
{
    /// <summary>Outbox processing plus a transport: RabbitMQ when an IConnection is registered, otherwise in-process.</summary>
    public static IServiceCollection AddClarioMessaging(this IServiceCollection services, IConfiguration configuration, bool useRabbitMq)
    {
        services.Configure<MessagingOptions>(configuration.GetSection(MessagingOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IntegrationEventDispatcher>();
        services.AddHostedService<OutboxProcessor>();

        if (useRabbitMq)
        {
            services.AddSingleton<IMessagePublisher, RabbitMqMessagePublisher>();
            services.AddHostedService<RabbitMqConsumerService>();
        }
        else
        {
            services.AddSingleton<IMessagePublisher, InProcessMessagePublisher>();
        }

        return services;
    }

    /// <summary>Subscribes a module handler. <typeparamref name="TDbContext"/> is the handling module's context (holds its inbox).</summary>
    public static IServiceCollection AddIntegrationEventHandler<TEvent, THandler, TDbContext>(this IServiceCollection services, string module)
        where TEvent : IIntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>
        where TDbContext : ModuleDbContext
    {
        services.AddScoped<THandler>();
        services.AddSingleton(new IntegrationEventHandlerRegistration(
            IntegrationEventNames.For(typeof(TEvent)),
            typeof(TEvent),
            typeof(THandler),
            typeof(TDbContext),
            $"clarioshop.{module}",
            (provider, integrationEvent, cancellationToken) =>
                provider.GetRequiredService<THandler>().HandleAsync((TEvent)integrationEvent, cancellationToken)));
        return services;
    }
}
