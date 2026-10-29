using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ClarioShop.Infrastructure.Auditing;

public static class AuditRegistration
{
    /// <summary>Registers <see cref="AuditTrail"/>; safe to call from every module that audits.</summary>
    public static IServiceCollection AddAuditTrail(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.TryAddScoped<AuditTrail>();
        return services;
    }
}
