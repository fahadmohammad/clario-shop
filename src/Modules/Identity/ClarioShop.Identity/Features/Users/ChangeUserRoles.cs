using System.Text.Json.Serialization;
using ClarioShop.Identity.Contracts;
using ClarioShop.Identity.Domain;
using ClarioShop.Identity.Persistence;
using ClarioShop.Infrastructure.Auditing;

namespace ClarioShop.Identity.Features.Users;

/// <summary>
/// Replaces a staff member's roles and records the change in the audit log. The new permissions reach their tokens
/// on the next refresh (within the 15-minute access token lifetime).
/// </summary>
public static class ChangeUserRoles
{
    public sealed record Command(string[] Roles)
    {
        /// <summary>Set by the endpoint from the route.</summary>
        [JsonIgnore]
        public Guid UserId { get; init; }
    }

    public sealed record Response(Guid Id, IReadOnlyList<string> Roles);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.Roles).NotEmpty();
            RuleForEach(command => command.Roles).NotEmpty().MaximumLength(100);
        }
    }

    internal sealed class Handler(IdentityDbContext db, AuditTrail audit) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            var wanted = command.Roles.Select(Role.Normalize).Distinct().ToList();
            var roles = await db.Roles.Where(role => wanted.Contains(role.NormalizedName)).ToListAsync(cancellationToken);
            if (roles.Count != wanted.Count)
            {
                return IdentityErrors.UnknownRoles(nameof(Command.Roles));
            }

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            // Serializes every change that could remove an Owner, so two Owners cannot demote each other at once.
            var owner = await db.Roles.SingleAsync(role => role.NormalizedName == Role.Normalize(SystemRoles.Owner), cancellationToken);
            await db.Database.ExecuteSqlAsync($"""SELECT 1 FROM identity."Roles" WHERE "Id" = {owner.Id} FOR UPDATE""", cancellationToken);

            var user = await db.Users.Include(u => u.Roles)
                .SingleOrDefaultAsync(u => u.Id == command.UserId && u.Email != null, cancellationToken);
            if (user is null)
            {
                return IdentityErrors.UserNotFound;
            }

            var losesOwner = user.Roles.Any(r => r.RoleId == owner.Id) && roles.All(r => r.Id != owner.Id);
            if (losesOwner && user.IsActive && !await AnotherActiveOwnerAsync(user.Id, owner.Id, cancellationToken))
            {
                return IdentityErrors.LastOwner;
            }

            var before = await RoleNamesAsync(user.Roles.Select(r => r.RoleId), cancellationToken);
            var after = roles.Select(role => role.Name).Order(StringComparer.Ordinal).ToList();
            if (user.SetRoles(roles))
            {
                audit.Record(db, AuditActions.UserRolesChanged, nameof(User), user.Id, new { Roles = before }, new { Roles = after });
                await db.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return new Response(user.Id, after);
        }

        private Task<bool> AnotherActiveOwnerAsync(Guid userId, Guid ownerRoleId, CancellationToken cancellationToken) =>
            db.Users.AnyAsync(u => u.Id != userId && u.IsActive && u.Roles.Any(r => r.RoleId == ownerRoleId), cancellationToken);

        private async Task<List<string>> RoleNamesAsync(IEnumerable<Guid> roleIds, CancellationToken cancellationToken)
        {
            var ids = roleIds.ToList();
            var names = await db.Roles.Where(role => ids.Contains(role.Id)).Select(role => role.Name).ToListAsync(cancellationToken);
            return names.Order(StringComparer.Ordinal).ToList();
        }
    }
}
