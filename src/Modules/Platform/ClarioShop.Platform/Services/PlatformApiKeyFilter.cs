using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace ClarioShop.Platform.Services;

/// <summary>Phase 0 guard for platform admin endpoints. Replaced by platform-admin sign-in with two-factor later.</summary>
internal sealed class PlatformApiKeyFilter(IOptions<PlatformOptions> options) : IEndpointFilter
{
    public const string Header = "X-Platform-Key";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var expected = options.Value.ApiKey;
        if (string.IsNullOrEmpty(expected))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "platform.disabled", detail: "Platform administration is not configured.");
        }

        var supplied = context.HttpContext.Request.Headers[Header].ToString();
        var matches = CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(expected));
        return matches ? await next(context) : TypedResults.Unauthorized();
    }
}
