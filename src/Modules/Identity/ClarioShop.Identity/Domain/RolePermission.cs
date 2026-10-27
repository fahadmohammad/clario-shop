namespace ClarioShop.Identity.Domain;

internal sealed class RolePermission
{
    public Guid RoleId { get; init; }

    public required string Permission { get; init; }
}
