using System.Security.Claims;
using ClarioShop.SharedKernel.Security;
using ClarioShop.SharedKernel.Tenancy;

namespace ClarioShop.Infrastructure.Security;

public static class ShopperPrincipal
{
    /// <summary>
    /// The Customers module's id of the signed-in shopper, read from the token's customer claim; null for guests, staff
    /// and tokens issued by another shop. Shopper endpoints take the customer from here, never from the request.
    /// </summary>
    public static Guid? ShopperCustomerId(this ClaimsPrincipal user, ITenantContext tenant)
    {
        if (user.Identity?.IsAuthenticated != true || !tenant.IsResolved)
        {
            return null;
        }

        var tokenTenant = user.FindFirst(ClarioClaimTypes.Tenant)?.Value;
        if (!string.Equals(tokenTenant, tenant.TenantId.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Guid.TryParse(user.FindFirst(ClarioClaimTypes.Customer)?.Value, out var customerId) ? customerId : null;
    }
}
