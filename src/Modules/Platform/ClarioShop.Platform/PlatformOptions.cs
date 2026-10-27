namespace ClarioShop.Platform;

public sealed class PlatformOptions
{
    public const string SectionName = "Platform";

    /// <summary>Key for the /api/platform endpoints (X-Platform-Key header). Empty disables them.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Server connection string tenant databases are created on. Defaults to ConnectionStrings:postgres.</summary>
    public string TenantDatabaseServer { get; set; } = string.Empty;

    public bool MigrateOnStartup { get; set; }

    public DemoTenantOptions DemoTenant { get; set; } = new();
}

/// <summary>Development seed: a ready-to-use shop at localhost.</summary>
public sealed class DemoTenantOptions
{
    public bool Enabled { get; set; }

    public string Identifier { get; set; } = "demo";

    public string Name { get; set; } = "Demo Supershop";

    public string[] Hosts { get; set; } = ["localhost", "demo.localhost"];

    public string OwnerEmail { get; set; } = "owner@demo.local";

    public string OwnerPassword { get; set; } = string.Empty;
}
