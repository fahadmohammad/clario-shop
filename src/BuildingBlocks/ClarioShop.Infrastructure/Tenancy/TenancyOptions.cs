namespace ClarioShop.Infrastructure.Tenancy;

public sealed class TenancyOptions
{
    public const string SectionName = "Tenancy";

    public const string TenantHeader = "X-Tenant";

    /// <summary>Allows choosing the tenant by the X-Tenant header (identifier). Always on in Development.</summary>
    public bool AllowTenantHeader { get; set; }

    /// <summary>Requests under these paths are not tenant-scoped.</summary>
    public string[] ExcludedPathPrefixes { get; set; } = ["/api/platform", "/health", "/alive", "/openapi", "/scalar"];
}
