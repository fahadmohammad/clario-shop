namespace ClarioShop.Identity.Domain;

/// <summary>Actions Identity writes to its audit log.</summary>
internal static class AuditActions
{
    public const string UserCreated = "identity.user.created";

    public const string UserRolesChanged = "identity.user.roles_changed";
}
