using Microsoft.AspNetCore.Authorization;

namespace ClarioShop.Infrastructure.Security;

/// <summary>Met by a signed-in shopper of the current tenant: a token for this shop that carries a customer id.</summary>
internal sealed class ShopperRequirement : IAuthorizationRequirement
{
    public const string PolicyName = "shopper";
}
