using ClarioShop.SharedKernel.Tenancy;

namespace ClarioShop.Platform.Domain;

internal static class TenantPlans
{
    public static PlanFeatures FeaturesOf(TenantPlan plan) => plan switch
    {
        TenantPlan.Online => PlanFeatures.OnlineStore,
        TenantPlan.Pos => PlanFeatures.Pos | PlanFeatures.MultipleLocations,
        TenantPlan.RetailSuite => PlanFeatures.OnlineStore | PlanFeatures.Pos | PlanFeatures.MultipleLocations,
        _ => PlanFeatures.None,
    };
}
