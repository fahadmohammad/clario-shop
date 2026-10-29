using ClarioShop.Platform.Services;

namespace ClarioShop.Platform.Features.Tenants;

internal sealed class TenantEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        // Platform admin is not tenant-scoped and uses the X-Platform-Key header, not a tenant token.
        var group = app.MapGroup("/api/platform/tenants")
            .WithTags("Platform")
            .AddEndpointFilter<PlatformApiKeyFilter>()
            .AllowAnonymous();

        group.MapPost("/", static async (CreateTenant.Command command, CreateTenant.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(command, cancellationToken))
                    .ToHttpResult(created => TypedResults.Created($"/api/platform/tenants/{created.Identifier}", created)))
            .WithName("CreateTenant")
            .WithSummary("Provision a new client shop: database, schemas, system roles and owner account.")
            .WithValidation<CreateTenant.Command>()
            .Produces<CreateTenant.Response>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/", static async (ListTenants.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new ListTenants.Query(), cancellationToken)).ToHttpResult())
            .WithName("ListTenants")
            .Produces<IReadOnlyList<ListTenants.TenantItem>>();

        group.MapPut("/{identifier}/plan", static async (string identifier, ChangeTenantPlan.Request request, ChangeTenantPlan.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new ChangeTenantPlan.Command(identifier, request.Plan), cancellationToken)).ToHttpResult())
            .WithName("ChangeTenantPlan")
            .WithSummary("Move a shop to another plan (Online, Pos, RetailSuite). Endpoints outside the new plan answer 403 within a minute on every server.")
            .WithValidation<ChangeTenantPlan.Request>()
            .Produces<ChangeTenantPlan.Response>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
