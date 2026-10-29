using ClarioShop.SharedKernel.Tenancy;

namespace ClarioShop.Infrastructure.Tenancy;

/// <summary>Refuses endpoints that need a feature the resolved tenant's plan does not include.</summary>
internal sealed class PlanGateMiddleware(RequestDelegate next)
{
    public const string ErrorCode = "plan_feature_unavailable";

    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext)
    {
        var requirements = context.GetEndpoint()?.Metadata.GetOrderedMetadata<PlanFeatureRequirement>() ?? [];
        var tenant = tenantContext.IsResolved ? tenantContext.Current : null;
        var missing = tenant is null ? null : requirements.FirstOrDefault(requirement => !tenant.Features.HasFlag(requirement.Feature));
        if (tenant is null || missing is null)
        {
            await next(context);
            return;
        }

        await Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: ErrorCode,
                detail: $"This shop's plan ({tenant.Plan}) does not include {Describe(missing.Feature)}.",
                extensions: new Dictionary<string, object?> { ["code"] = ErrorCode, ["feature"] = missing.Feature.ToString() })
            .ExecuteAsync(context);
    }

    private static string Describe(PlanFeatures feature) => feature switch
    {
        PlanFeatures.OnlineStore => "the online store",
        PlanFeatures.Pos => "POS",
        PlanFeatures.MultipleLocations => "more than one location",
        _ => feature.ToString(),
    };
}
