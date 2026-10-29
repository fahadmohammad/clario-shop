using System.Text.Json.Serialization;

namespace ClarioShop.Platform;

/// <summary>What a client pays for. Each plan turns parts of the product on (see <c>TenantPlans</c>).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TenantPlan>))]
public enum TenantPlan
{
    /// <summary>Storefront, mobile app, online orders and reports, one location.</summary>
    Online,

    /// <summary>POS and back-office inventory, one or more locations.</summary>
    Pos,

    /// <summary>Online and POS on one inventory, with multiple locations.</summary>
    RetailSuite,
}
