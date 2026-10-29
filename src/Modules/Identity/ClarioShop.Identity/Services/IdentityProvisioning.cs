using ClarioShop.Identity.Contracts;
using ClarioShop.Identity.Domain;
using ClarioShop.Identity.Persistence;
using ClarioShop.Infrastructure.Auditing;
using Microsoft.AspNetCore.Identity;

namespace ClarioShop.Identity.Services;

internal sealed class IdentityProvisioning(
    IdentityDbContext db,
    SystemRoleSynchronizer roles,
    IPasswordHasher<User> passwordHasher,
    AuditTrail audit,
    TimeProvider clock) : IIdentityProvisioning
{
    public async Task<Result> CreateOwnerAsync(string email, string fullName, string password, CancellationToken cancellationToken)
    {
        await roles.InitializeAsync(cancellationToken);

        var normalized = User.Normalize(email);
        if (await db.Users.AnyAsync(user => user.NormalizedEmail == normalized, cancellationToken))
        {
            return Result.Success();
        }

        var owner = User.Create(email, fullName, clock.GetUtcNow());
        owner.SetPasswordHash(passwordHasher.HashPassword(owner, password));
        var ownerRole = await db.Roles.SingleAsync(role => role.NormalizedName == Role.Normalize(SystemRoles.Owner), cancellationToken);
        owner.AssignRole(ownerRole);

        db.Users.Add(owner);
        audit.Record(db, AuditActions.UserCreated, nameof(User), owner.Id, null, new { owner.Email, owner.FullName, Roles = new[] { ownerRole.Name } });
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
