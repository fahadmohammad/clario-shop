namespace ClarioShop.SharedKernel.Security;

/// <summary>Each module declares its permissions so Identity can seed roles and validate role definitions.</summary>
public interface IPermissionProvider
{
    IReadOnlyCollection<PermissionDefinition> GetPermissions();
}

public sealed record PermissionDefinition(string Name, string Module, string Description);
