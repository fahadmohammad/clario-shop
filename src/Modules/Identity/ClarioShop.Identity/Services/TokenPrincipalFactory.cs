using System.Security.Claims;
using ClarioShop.Identity.Domain;
using ClarioShop.Identity.Persistence;
using ClarioShop.SharedKernel.Security;
using ClarioShop.SharedKernel.Tenancy;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace ClarioShop.Identity.Services;

/// <summary>Builds the principal OpenIddict turns into tokens: the user, the tenant and the user's current permissions.</summary>
internal sealed class TokenPrincipalFactory(IdentityDbContext db, ITenantContext tenant, IOptions<AuthOptions> options)
{
    public async Task<ClaimsPrincipal> CreateAsync(User user, CancellationToken cancellationToken)
    {
        var roleIds = user.Roles.Select(role => role.RoleId).ToList();
        var permissions = await db.Roles
            .Where(role => roleIds.Contains(role.Id))
            .SelectMany(role => role.Permissions.Select(permission => permission.Permission))
            .Distinct()
            .ToListAsync(cancellationToken);

        var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, Claims.Name, Claims.Role);
        identity.SetClaim(Claims.Subject, user.Id.ToString())
            .SetClaim(Claims.Email, user.Email)
            .SetClaim(Claims.PhoneNumber, user.Phone)
            .SetClaim(ClarioClaimTypes.Customer, user.CustomerId?.ToString())
            .SetClaim(Claims.Name, user.FullName)
            .SetClaim(ClarioClaimTypes.Tenant, tenant.TenantId.ToString())
            .SetClaims(ClarioClaimTypes.Permission, [.. permissions]);

        // Every sign-in gets a refresh token; access tokens are for this API only.
        identity.SetScopes(Scopes.OfflineAccess);
        identity.SetResources(options.Value.Audience);
        identity.SetDestinations(static _ => [Destinations.AccessToken]);
        return new ClaimsPrincipal(identity);
    }
}
