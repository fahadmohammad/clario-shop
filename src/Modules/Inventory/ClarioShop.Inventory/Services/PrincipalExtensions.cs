using System.Security.Claims;

namespace ClarioShop.Inventory.Services;

internal static class PrincipalExtensions
{
    /// <summary>The signed-in user's id (the token's sub claim), recorded on ledger entries.</summary>
    public static Guid? UserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirst("sub")?.Value, out var id) ? id : null;
}
