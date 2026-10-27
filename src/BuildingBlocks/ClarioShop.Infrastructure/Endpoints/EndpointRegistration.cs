using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ClarioShop.Infrastructure.Endpoints;

public static class EndpointRegistration
{
    public static IServiceCollection AddEndpointGroups(this IServiceCollection services, Assembly assembly)
    {
        var groups = assembly.DefinedTypes.Where(type =>
            type is { IsAbstract: false, IsInterface: false } && type.IsAssignableTo(typeof(IEndpointGroup)));

        foreach (var group in groups)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IEndpointGroup), group));
        }

        return services;
    }

    public static IEndpointRouteBuilder MapEndpoints(this IEndpointRouteBuilder app)
    {
        foreach (var group in app.ServiceProvider.GetServices<IEndpointGroup>())
        {
            group.Map(app);
        }

        return app;
    }
}
