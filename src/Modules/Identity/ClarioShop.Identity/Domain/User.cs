using ClarioShop.SharedKernel.Domain;

namespace ClarioShop.Identity.Domain;

internal sealed class User : Entity
{
    private readonly List<UserRole> roles = [];

    private User()
    {
    }

    /// <summary>Staff sign in with it; shoppers who signed in by phone have none.</summary>
    public string? Email { get; private set; }

    public string? NormalizedEmail { get; private set; }

    /// <summary>E.164 mobile number a shopper signs in with by one-time code.</summary>
    public string? Phone { get; private set; }

    /// <summary>The shopper's customer in the Customers module, matched by phone.</summary>
    public Guid? CustomerId { get; private set; }

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

    /// <summary>A shopper account for a phone number, linked to the customer with that number. Has no roles.</summary>
    public static User CreateShopper(string phone, Guid customerId, string? fullName, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        Phone = phone,
        CustomerId = customerId,
        FullName = fullName?.Trim() ?? string.Empty,
        IsActive = true,
        CreatedAt = now,
    };

    public static string Normalize(string email) => email.Trim().ToUpperInvariant();

    public void SetPasswordHash(string passwordHash) => PasswordHash = passwordHash;

    public void LinkCustomer(Guid customerId) => CustomerId = customerId;

    /// <summary>Replaces the user's roles; false when they already had exactly these.</summary>
    public bool SetRoles(IReadOnlyCollection<Role> wanted)
    {
        var wantedIds = wanted.Select(role => role.Id).ToHashSet();
        if (wantedIds.SetEquals(roles.Select(existing => existing.RoleId)))
        {
            return false;
        }

        roles.RemoveAll(existing => !wantedIds.Contains(existing.RoleId));
        foreach (var role in wanted)
        {
            AssignRole(role);
        }

        return true;
    }

    public void AssignRole(Role role)
    {
        if (roles.All(existing => existing.RoleId != role.Id))
        {
            roles.Add(new UserRole { UserId = Id, RoleId = role.Id });
        }
    }
}
