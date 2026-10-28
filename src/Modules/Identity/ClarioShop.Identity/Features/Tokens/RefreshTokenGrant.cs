using System.Security.Claims;
using ClarioShop.Identity.Persistence;
using ClarioShop.Identity.Services;
using ClarioShop.SharedKernel.Security;
using ClarioShop.SharedKernel.Tenancy;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace ClarioShop.Identity.Features.Tokens;

/// <summary>
/// grant_type=refresh_token: OpenIddict has already validated and rotated the refresh token. The user must still be
/// active, and the new tokens carry the user's permissions as they are now.
/// </summary>
public static class RefreshTokenGrant
{
    public sealed record Command(ClaimsPrincipal? RefreshTokenPrincipal);

    internal sealed class Handler(
        IdentityDbContext db,
        TokenPrincipalFactory principals,
        ITenantContext tenant) : ICommandHandler<Command, ClaimsPrincipal>
    {
        private static readonly Error Rejected =
            Error.Unauthorized("identity.refresh_rejected", "The refresh token is no longer valid.");

        public async Task<Result<ClaimsPrincipal>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            var principal = command.RefreshTokenPrincipal;
            if (principal is null
                || !string.Equals(principal.GetClaim(ClarioClaimTypes.Tenant), tenant.TenantId.ToString(), StringComparison.OrdinalIgnoreCase)
                || !Guid.TryParse(principal.GetClaim(Claims.Subject), out var userId))
            {
                return Rejected;
            }

            var user = await db.Users.Include(u => u.Roles)
                .SingleOrDefaultAsync(u => u.Id == userId && u.IsActive, cancellationToken);
            if (user is null)
            {
                return Rejected;
            }

            return await principals.CreateAsync(user, cancellationToken);
        }
    }
}
