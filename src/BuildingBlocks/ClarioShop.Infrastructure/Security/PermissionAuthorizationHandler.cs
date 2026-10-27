using ClarioShop.SharedKernel.Security;
using ClarioShop.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Authorization;

namespace ClarioShop.Infrastructure.Security;

/// <summary>Grants a permission only when the token belongs to the resolved tenant and carries the permission.</summary>
internal sealed class PermissionAuthorizationHandler(ITenantContext tenant) : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (!tenant.IsResolved)
        {
            return Task.CompletedTask;
        }

        var tokenTenant = context.User.FindFirst(ClarioClaimTypes.Tenant)?.Value;
        if (!string.Equals(tokenTenant, tenant.TenantId.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        if (context.User.HasClaim(ClarioClaimTypes.Permission, requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
