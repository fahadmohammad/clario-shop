using ClarioShop.Identity.Domain;
using ClarioShop.Identity.Persistence;
using ClarioShop.Identity.Services;
using ClarioShop.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Identity;

namespace ClarioShop.Identity.Features.Tokens;

public static class IssueToken
{
    public sealed record Command(string Email, string Password);

    public sealed record Response(string AccessToken, string TokenType, DateTimeOffset ExpiresAt);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.Email).NotEmpty().EmailAddress();
            RuleFor(command => command.Password).NotEmpty();
        }
    }

    internal sealed class Handler(
        IdentityDbContext db,
        IPasswordHasher<User> passwordHasher,
        TokenService tokens,
        ITenantContext tenant) : ICommandHandler<Command, Response>
    {
        private static readonly Error InvalidCredentials =
            Error.Unauthorized("identity.invalid_credentials", "The email or password is incorrect.");

        public async Task<Result<Response>> HandleAsync(Command command, CancellationToken cancellationToken)
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

            var roleIds = user.Roles.Select(role => role.RoleId).ToList();
            var permissions = await db.Roles
                .Where(role => roleIds.Contains(role.Id))
                .SelectMany(role => role.Permissions.Select(permission => permission.Permission))
                .Distinct()
                .ToListAsync(cancellationToken);

            var token = tokens.Create(user, permissions, tenant.TenantId);
            return new Response(token.Value, "Bearer", token.ExpiresAt);
        }
    }
}
