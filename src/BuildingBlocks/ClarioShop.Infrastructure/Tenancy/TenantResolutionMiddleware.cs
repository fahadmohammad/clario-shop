namespace ClarioShop.Infrastructure.Tenancy;

internal sealed class TenantResolutionMiddleware(
    RequestDelegate next,
    IOptions<TenancyOptions> options,
    IHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context, ITenantStore store, TenantContext tenantContext)
    {
        var path = context.Request.Path;
        if (options.Value.ExcludedPathPrefixes.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        var tenant = await ResolveAsync(context, store);
        if (tenant is not { IsActive: true })
        {
            await Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "tenant_not_found",
                    detail: "No active shop is configured for this address.")
                .ExecuteAsync(context);
            return;
        }

        tenantContext.Set(tenant);
        await next(context);
    }

    private Task<TenantInfo?> ResolveAsync(HttpContext context, ITenantStore store)
    {
        var headerAllowed = options.Value.AllowTenantHeader || environment.IsDevelopment();
        if (headerAllowed && context.Request.Headers.TryGetValue(TenancyOptions.TenantHeader, out var identifier)
            && !string.IsNullOrWhiteSpace(identifier))
        {
            return store.FindByIdentifierAsync(identifier.ToString(), context.RequestAborted);
        }

        return store.FindByHostAsync(context.Request.Host.Host, context.RequestAborted);
    }
}
