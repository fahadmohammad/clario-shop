using ClarioShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Design;

namespace ClarioShop.Reporting.Persistence;

internal sealed class ReportingDesignTimeFactory : IDesignTimeDbContextFactory<ReportingDbContext>
{
    public ReportingDbContext CreateDbContext(string[] args) =>
        new(TenantDbContextRegistration.DesignTimeOptions<ReportingDbContext>(ReportingDbContext.SchemaName));
}
