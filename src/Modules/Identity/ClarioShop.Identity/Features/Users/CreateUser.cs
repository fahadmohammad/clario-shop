using ClarioShop.Identity.Domain;
using ClarioShop.Identity.Persistence;
using Microsoft.AspNetCore.Identity;

namespace ClarioShop.Identity.Features.Users;

public static class CreateUser
{
    public sealed record Command(string Email, string FullName, string Password, string[] Roles);

    public sealed record Response(Guid Id, string Email);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(256);
            RuleFor(command => command.FullName).NotEmpty().MaximumLength(200);
            RuleFor(command => command.Password)
                .NotEmpty()
                .MinimumLength(8)
                .Matches("[A-Za-z]").WithMessage("Password must contain a letter.")
                .Matches("[0-9]").WithMessage("Password must contain a digit.");
            RuleFor(command => command.Roles).NotEmpty();
        }
    }

    internal sealed class Handler(IdentityDbContext db, IPasswordHasher<User> passwordHasher, TimeProvider clock)
        : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            var normalized = User.Normalize(command.Email);
            if (await db.Users.AnyAsync(user => user.NormalizedEmail == normalized, cancellationToken))
            {
                return Error.Conflict("identity.email_taken", "A user with this email already exists.");
            }

            var wanted = command.Roles.Select(Role.Normalize).Distinct().ToList();
            var roles = await db.Roles.Where(role => wanted.Contains(role.NormalizedName)).ToListAsync(cancellationToken);
            if (roles.Count != wanted.Count)
            {
                return Error.Validation(nameof(Command.Roles), "One or more roles do not exist.");
            }

            var user = User.Create(command.Email, command.FullName, clock.GetUtcNow());
            user.SetPasswordHash(passwordHasher.HashPassword(user, command.Password));
            roles.ForEach(user.AssignRole);

            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken);
            return new Response(user.Id, command.Email.Trim());
        }
    }
}
