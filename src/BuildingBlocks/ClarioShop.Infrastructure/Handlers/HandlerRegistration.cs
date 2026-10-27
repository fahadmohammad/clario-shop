using System.Reflection;
using ClarioShop.SharedKernel.Handlers;
using FluentValidation;

namespace ClarioShop.Infrastructure.Handlers;

public static class HandlerRegistration
{
    /// <summary>Registers every command handler (as itself and its interface) and every validator in the module assembly.</summary>
    public static IServiceCollection AddModuleHandlers(this IServiceCollection services, Assembly assembly)
    {
        var handlerInterface = typeof(ICommandHandler<,>);

        foreach (var type in assembly.DefinedTypes.Where(t => t is { IsAbstract: false, IsInterface: false }))
        {
            var interfaces = type.ImplementedInterfaces
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterface)
                .ToList();

            if (interfaces.Count == 0)
            {
                continue;
            }

            services.AddScoped(type);
            foreach (var implemented in interfaces)
            {
                services.AddScoped(implemented, provider => provider.GetRequiredService(type));
            }
        }

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        return services;
    }
}
