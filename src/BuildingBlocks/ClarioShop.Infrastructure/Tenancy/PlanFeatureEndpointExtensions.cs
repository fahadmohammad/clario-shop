using ClarioShop.SharedKernel.Tenancy;

namespace ClarioShop.Infrastructure.Tenancy;

public static class PlanFeatureEndpointExtensions
{
    /// <summary>
    /// Answers 403 with a <c>plan_feature_unavailable</c> ProblemDetails in a shop whose plan does not include
    /// <paramref name="feature"/>. Checked after authentication, so a caller without a valid token still gets 401.
    /// </summary>
    public static TBuilder RequirePlanFeature<TBuilder>(this TBuilder builder, PlanFeatures feature)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new PlanFeatureRequirement(feature));
}
