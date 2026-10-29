using ClarioShop.Catalog.Contracts;
using ClarioShop.Infrastructure.Auditing;

namespace ClarioShop.Catalog.Features.Audit;

internal sealed class AuditEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/catalog/audit-log").WithTags("Catalog")
            .MapGet("/", static async ([AsParameters] ListAuditLog.Query query, ListAuditLog.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(query, cancellationToken)).ToHttpResult())
            .WithName("ListCatalogAuditLog")
            .WithSummary("Who changed which variant's price, when, from which device, and the price before and after, newest first.")
            .WithValidation<ListAuditLog.Query>()
            .Produces<AuditLogPage>()
            .RequirePermission(CatalogPermissions.AuditView);
    }
}
