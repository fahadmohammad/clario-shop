using ClarioShop.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ClarioShop.Infrastructure.Persistence;

public static class TenantDbContextRegistration
{
    /// <summary>Registers a module DbContext that connects to the current tenant's database.</summary>
    public static IServiceCollection AddTenantDbContext<TContext>(this IServiceCollection services, string schema)
        where TContext : ModuleDbContext
    {
        services.AddDbContext<TContext>((provider, options) =>
        {
            var tenant = provider.GetRequiredService<ITenantContext>();
            if (!tenant.IsResolved)
            {
                throw new InvalidOperationException(
                    $"{typeof(TContext).Name} needs a tenant, but none is resolved for this scope.");
            }

            options.UseNpgsql(tenant.ConnectionString, npgsql =>
                npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, schema));
        });

        services.AddSingleton(new ModuleDbContextRegistration(typeof(TContext), schema));
        return services;
    }

    /// <summary>Options for design-time tools (dotnet ef), which run without a tenant.</summary>
    public static DbContextOptions<TContext> DesignTimeOptions<TContext>(string schema)
        where TContext : DbContext =>
        new DbContextOptionsBuilder<TContext>()
            .UseNpgsql(
                "Host=localhost;Database=clarioshop_design;Username=postgres;Password=postgres",
                npgsql => npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, schema))
            .Options;
}
