namespace ClarioShop.Identity.Domain;

internal sealed class UserRole
{
    public Guid UserId { get; init; }

    public Guid RoleId { get; init; }
}
