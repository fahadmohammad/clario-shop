using System.Text.Json.Serialization;

namespace ClarioShop.SharedKernel.Tenancy;

/// <summary>
/// Parts of the product a tenant's plan can include. Endpoints that need one declare it with
/// <c>RequirePlanFeature</c>; in a shop whose plan lacks it they answer 403.
/// </summary>
[Flags]
[JsonConverter(typeof(JsonStringEnumConverter<PlanFeatures>))]
public enum PlanFeatures
{
    None = 0,

    /// <summary>Storefront and app shopping: carts, checkout, online orders and their payments, shopper accounts.</summary>
    OnlineStore = 1,

    /// <summary>Tills, shifts and in-store sales.</summary>
    Pos = 2,

    /// <summary>More than the default location: branches, warehouses and transfers between them.</summary>
    MultipleLocations = 4,
}
