using ClarioShop.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Authorization;

namespace ClarioShop.Infrastructure.Security;

internal sealed class ShopperAuthorizationHandler(ITenantContext tenant) : AuthorizationHandler<ShopperRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ShopperRequirement requirement)
    {
        if (context.User.ShopperCustomerId(tenant) is not null)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
