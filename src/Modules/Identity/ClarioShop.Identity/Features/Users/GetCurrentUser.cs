using System.Security.Claims;
using ClarioShop.Identity.Persistence;
using ClarioShop.SharedKernel.Security;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace ClarioShop.Identity.Features.Users;

public static class GetCurrentUser
{
    public sealed record Query(ClaimsPrincipal Principal);

    public sealed record Response(Guid Id, string Email, string FullName, IReadOnlyList<string> Permissions);

    internal sealed class Handler(IdentityDbContext db) : ICommandHandler<Query, Response>
    {
        public async Task<Result<Response>> HandleAsync(Query query, CancellationToken cancellationToken)
        {
            if (!Guid.TryParse(query.Principal.FindFirst(Claims.Subject)?.Value, out var userId))
            {
                return Error.Unauthorized("identity.no_subject", "The token has no user.");
            }

            var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId && u.IsActive, cancellationToken);
            if (user is null)
            {
                return Error.NotFound("identity.user_not_found", "The signed-in user no longer exists.");
            }

            var permissions = query.Principal.FindAll(ClarioClaimTypes.Permission).Select(claim => claim.Value).Order().ToList();
            return new Response(user.Id, user.Email, user.FullName, permissions);
        }
    }
}
