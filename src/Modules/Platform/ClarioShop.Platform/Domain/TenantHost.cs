namespace ClarioShop.Platform.Domain;

/// <summary>A host name (domain) that serves one tenant's storefront and API.</summary>
internal sealed class TenantHost
{
    public required string Host { get; init; }

    public Guid TenantId { get; init; }

    public static string Normalize(string host) => host.Trim().TrimEnd('.').ToLowerInvariant();
}
