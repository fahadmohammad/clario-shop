using System.Security.Claims;
using ClarioShop.Identity.Domain;
using ClarioShop.Identity.Persistence;
using ClarioShop.Identity.Services;
using Microsoft.AspNetCore.Identity;

namespace ClarioShop.Identity.Features.Tokens;

/// <summary>grant_type=password: a staff member signs in with email and password.</summary>
public static class PasswordGrant
{
    public sealed record Command(string Email, string Password);

    internal sealed class Handler(
        IdentityDbContext db,
        IPasswordHasher<User> passwordHasher,
        TokenPrincipalFactory principals) : ICommandHandler<Command, ClaimsPrincipal>
    {
        private static readonly Error InvalidCredentials =
            Error.Unauthorized("identity.invalid_credentials", "The email or password is incorrect.");

        public async Task<Result<ClaimsPrincipal>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            var normalized = User.Normalize(command.Email);
            var user = await db.Users.Include(u => u.Roles)
                .SingleOrDefaultAsync(u => u.NormalizedEmail == normalized && u.IsActive, cancellationToken);
            if (user is null)
            {
                return InvalidCredentials;
            }

            var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, command.Password);
            if (verification == PasswordVerificationResult.Failed)
            {
                return InvalidCredentials;
            }

            if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.SetPasswordHash(passwordHasher.HashPassword(user, command.Password));
                await db.SaveChangesAsync(cancellationToken);
            }

            return await principals.CreateAsync(user, cancellationToken);
        }
    }
}
