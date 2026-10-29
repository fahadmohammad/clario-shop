using ClarioShop.Platform.Persistence;
using ClarioShop.Platform.Services;
using Microsoft.Extensions.Caching.Hybrid;

namespace ClarioShop.Platform.Features.Tenants;

public static class ChangeTenantPlan
{
    public sealed record Request(TenantPlan Plan);

    public sealed record Command(string Identifier, TenantPlan Plan);

    public sealed record Response(string Identifier, TenantPlan Plan);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator() => RuleFor(request => request.Plan).IsInEnum();
    }

    internal sealed class Handler(PlatformDbContext db, HybridCache cache) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            var identifier = command.Identifier.Trim().ToLowerInvariant();
            var tenant = await db.Tenants.SingleOrDefaultAsync(t => t.Identifier == identifier, cancellationToken);
            if (tenant is null)
            {
                return Error.NotFound("platform.tenant_not_found", $"No tenant called '{identifier}' exists.");
            }

            tenant.ChangePlan(command.Plan);
            await db.SaveChangesAsync(cancellationToken);
            await cache.RemoveByTagAsync(TenantStore.CacheTag, cancellationToken);
            return new Response(tenant.Identifier, tenant.Plan);
        }
    }
}
