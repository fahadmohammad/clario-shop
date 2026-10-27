using ClarioShop.Identity.Contracts;
using ClarioShop.Infrastructure.Tenancy;
using ClarioShop.Platform.Domain;
using ClarioShop.Platform.Persistence;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;

namespace ClarioShop.Platform.Services;

/// <summary>Creates a tenant end to end: registry row, database with every module's schema, owner account.</summary>
internal sealed class TenantProvisioner(
    PlatformDbContext db,
    TenantMigrator migrator,
    TenantScopeFactory tenantScopes,
    HybridCache cache,
    IOptions<PlatformOptions> options,
    IConfiguration configuration,
    TimeProvider clock)
{
    public async Task<Result<TenantInfo>> ProvisionAsync(TenantRequest request, CancellationToken cancellationToken)
    {
        var identifier = request.Identifier.Trim().ToLowerInvariant();
        if (await db.Tenants.AnyAsync(t => t.Identifier == identifier, cancellationToken))
        {
            return Error.Conflict("platform.identifier_taken", $"A tenant called '{identifier}' already exists.");
        }

        var hosts = request.Hosts.Select(TenantHost.Normalize).Distinct().ToList();
        var takenHost = await db.TenantHosts.Where(h => hosts.Contains(h.Host)).Select(h => h.Host).FirstOrDefaultAsync(cancellationToken);
        if (takenHost is not null)
        {
            return Error.Conflict("platform.host_taken", $"The host '{takenHost}' already belongs to another tenant.");
        }

        var server = string.IsNullOrWhiteSpace(options.Value.TenantDatabaseServer)
            ? configuration.GetConnectionString("postgres")
            : options.Value.TenantDatabaseServer;
        if (string.IsNullOrWhiteSpace(server))
        {
            return Error.Failure("platform.no_database_server", "No database server is configured for tenant databases.");
        }

        var connectionString = new NpgsqlConnectionStringBuilder(server)
        {
            Database = $"clarioshop_{identifier.Replace('-', '_')}",
        }.ConnectionString;

        var tenant = Tenant.Create(identifier, request.Name, connectionString, hosts, clock.GetUtcNow());
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(cancellationToken);

        var info = new TenantInfo(tenant.Id, tenant.Identifier, tenant.Name, tenant.ConnectionString, IsActive: true);
        await migrator.MigrateAsync(info, cancellationToken);

        await using (var scope = tenantScopes.CreateScope(info))
        {
            var owner = await scope.ServiceProvider.GetRequiredService<IIdentityProvisioning>()
                .CreateOwnerAsync(request.OwnerEmail, request.OwnerName, request.OwnerPassword, cancellationToken);
            if (owner.IsFailure)
            {
                return owner.Error;
            }
        }

        tenant.Activate();
        await db.SaveChangesAsync(cancellationToken);
        await cache.RemoveByTagAsync(TenantStore.CacheTag, cancellationToken);
        return info;
    }
}

internal sealed record TenantRequest(
    string Identifier,
    string Name,
    IReadOnlyList<string> Hosts,
    string OwnerEmail,
    string OwnerName,
    string OwnerPassword);
