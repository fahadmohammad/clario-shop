using System.Threading.RateLimiting;
using ClarioShop.Identity;
using ClarioShop.Infrastructure.Tenancy;

namespace ClarioShop.Api.Setup;

internal static class RateLimitingSetup
{
    /// <summary>Sign-in attempts per shop per IP address per minute (RateLimiting:SignInPerMinute, default 10).</summary>
    public static IServiceCollection AddClarioRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var permitLimit = configuration.GetValue("RateLimiting:SignInPerMinute", 10);

        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(IdentityModule.TokenRateLimitPolicy, context =>
            {
                var tenant = context.RequestServices.GetRequiredService<TenantContext>();
                var shop = tenant.IsResolved ? tenant.Identifier : "-";
                var address = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(
                    $"{shop}|{address}",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = TimeSpan.FromMinutes(1) });
            });
        });
    }
}
