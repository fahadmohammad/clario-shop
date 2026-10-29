using ClarioShop.SharedKernel.Tenancy;

namespace ClarioShop.Infrastructure.Tenancy;

/// <summary>Endpoint metadata: the tenant's plan must include <paramref name="Feature"/>.</summary>
public sealed record PlanFeatureRequirement(PlanFeatures Feature);
