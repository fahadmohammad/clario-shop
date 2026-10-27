using ClarioShop.SharedKernel.Domain;

namespace ClarioShop.Identity.Domain;

internal sealed class User : Entity
{
    private readonly List<UserRole> roles = [];

    private User()
    {
    }

    public string Email { get; private set; } = string.Empty;

    public string NormalizedEmail { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<UserRole> Roles => roles;

    public static User Create(string email, string fullName, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        Email = email.Trim(),
        NormalizedEmail = Normalize(email),
        FullName = fullName.Trim(),
        IsActive = true,
        CreatedAt = now,
    };

    public static string Normalize(string email) => email.Trim().ToUpperInvariant();

    public void SetPasswordHash(string passwordHash) => PasswordHash = passwordHash;

    public void AssignRole(Role role)
    {
        if (roles.All(existing => existing.RoleId != role.Id))
        {
            roles.Add(new UserRole { UserId = Id, RoleId = role.Id });
        }
    }
}
