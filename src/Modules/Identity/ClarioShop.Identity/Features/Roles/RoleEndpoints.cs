using ClarioShop.Identity.Contracts;

namespace ClarioShop.Identity.Features.Roles;

internal sealed class RoleEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/identity").WithTags("Identity")
            .MapGet("/roles", static async (ListRoles.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new ListRoles.Query(), cancellationToken)).ToHttpResult())
            .WithName("ListRoles")
            .WithSummary("Roles of this shop and the permissions each grants.")
            .Produces<IReadOnlyList<ListRoles.RoleItem>>()
            .RequirePermission(IdentityPermissions.RolesView);
    }
}
