namespace ClarioShop.Infrastructure.Security;

public static class PermissionEndpointExtensions
{
    /// <summary>Requires an authenticated user of the current tenant who holds <paramref name="permission"/>.</summary>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(PermissionPolicyProvider.Prefix + permission);
}
