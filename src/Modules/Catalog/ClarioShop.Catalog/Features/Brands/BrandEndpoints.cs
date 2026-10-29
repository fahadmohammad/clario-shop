using ClarioShop.Catalog.Contracts;

namespace ClarioShop.Catalog.Features.Brands;

internal sealed class BrandEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/catalog/brands").WithTags("Catalog");

        group.MapGet("/", static async (ListBrands.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new ListBrands.Query(), cancellationToken)).ToHttpResult())
            .WithName("ListBrands")
            .WithSummary("All brands by name, for the storefront brand filter and the back office.")
            .Produces<IReadOnlyList<BrandItem>>()
            .AllowAnonymous();

        group.MapPost("/", static async (CreateBrand.Command command, CreateBrand.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(command, cancellationToken))
                    .ToHttpResult(created => TypedResults.Created($"/api/catalog/brands/{created.Id}", created)))
            .WithName("CreateBrand")
            .WithSummary("Add a brand. Names are unique per shop, ignoring case.")
            .WithValidation<CreateBrand.Command>()
            .Produces<BrandItem>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(CatalogPermissions.BrandsEdit);

        group.MapPut("/{id:guid}", static async (Guid id, RenameBrand.Request brand, RenameBrand.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new RenameBrand.Command(id, brand), cancellationToken)).ToHttpResult())
            .WithName("RenameBrand")
            .WithSummary("Rename a brand. Its slug stays, so storefront filter links keep working.")
            .WithValidation<RenameBrand.Request>()
            .Produces<BrandItem>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(CatalogPermissions.BrandsEdit);
    }
}
