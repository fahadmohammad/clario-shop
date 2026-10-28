using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ClarioShop.Infrastructure.Sms;

public static class SmsRegistration
{
    /// <summary>The logging SMS sender, unless a real gateway's <see cref="ISmsSender"/> is already registered.</summary>
    public static IServiceCollection AddClarioSms(this IServiceCollection services)
    {
        services.TryAddSingleton<ISmsSender, LoggingSmsSender>();
        return services;
    }
}
