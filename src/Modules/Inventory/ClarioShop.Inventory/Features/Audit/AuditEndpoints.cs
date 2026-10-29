using ClarioShop.Infrastructure.Auditing;
using ClarioShop.Inventory.Contracts;

namespace ClarioShop.Inventory.Features.Audit;

internal sealed class AuditEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/inventory/audit-log").WithTags("Inventory")
            .MapGet("/", static async ([AsParameters] ListAuditLog.Query query, ListAuditLog.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(query, cancellationToken)).ToHttpResult())
            .WithName("ListInventoryAuditLog")
            .WithSummary("Who adjusted stock, when, from which device, and the stock on hand before and after, newest first.")
            .WithValidation<ListAuditLog.Query>()
            .Produces<AuditLogPage>()
            .RequirePermission(InventoryPermissions.AuditView);
    }
}
