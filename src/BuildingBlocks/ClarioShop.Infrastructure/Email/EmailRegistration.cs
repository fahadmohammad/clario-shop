using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ClarioShop.Infrastructure.Email;

public static class EmailRegistration
{
    /// <summary>The logging email sender, unless a real provider's <see cref="IEmailSender"/> is already registered.</summary>
    public static IServiceCollection AddClarioEmail(this IServiceCollection services)
    {
        services.TryAddSingleton<IEmailSender, LoggingEmailSender>();
        return services;
    }
}
