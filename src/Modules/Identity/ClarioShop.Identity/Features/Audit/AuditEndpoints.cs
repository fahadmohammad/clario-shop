using ClarioShop.Identity.Contracts;
using ClarioShop.Infrastructure.Auditing;

namespace ClarioShop.Identity.Features.Audit;

internal sealed class AuditEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/identity/audit-log").WithTags("Identity")
            .MapGet("/", static async ([AsParameters] ListAuditLog.Query query, ListAuditLog.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(query, cancellationToken)).ToHttpResult())
            .WithName("ListIdentityAuditLog")
            .WithSummary("Staff accounts created and role changes: who, when, from which device, and the roles before and after, newest first.")
            .WithValidation<ListAuditLog.Query>()
            .Produces<AuditLogPage>()
            .RequirePermission(IdentityPermissions.AuditView);
    }
}
