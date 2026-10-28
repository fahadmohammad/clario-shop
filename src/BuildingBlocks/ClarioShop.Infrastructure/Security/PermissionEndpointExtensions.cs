namespace ClarioShop.Infrastructure.Security;

public static class PermissionEndpointExtensions
{
    /// <summary>Requires an authenticated user of the current tenant who holds <paramref name="permission"/>.</summary>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(PermissionPolicyProvider.Prefix + permission);

    /// <summary>
    /// Requires a signed-in shopper of the current tenant (see <see cref="ShopperPrincipal.ShopperCustomerId"/>).
    /// The endpoint must still scope everything it reads or changes to that shopper's customer id.
    /// </summary>
    public static TBuilder RequireShopper<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(ShopperRequirement.PolicyName);
}
