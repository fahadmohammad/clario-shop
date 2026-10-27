using ClarioShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Design;

namespace ClarioShop.Platform.Persistence;

internal sealed class PlatformDesignTimeFactory : IDesignTimeDbContextFactory<PlatformDbContext>
{
    public PlatformDbContext CreateDbContext(string[] args) =>
        new(TenantDbContextRegistration.DesignTimeOptions<PlatformDbContext>(PlatformDbContext.SchemaName));
}
