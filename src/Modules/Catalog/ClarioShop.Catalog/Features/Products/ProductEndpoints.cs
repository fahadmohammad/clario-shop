using ClarioShop.Catalog.Contracts;

namespace ClarioShop.Catalog.Features.Products;

internal sealed class ProductEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/catalog/products").WithTags("Catalog");

        group.MapGet("/", static async ([AsParameters] ListProducts.Query query, ListProducts.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(query, cancellationToken)).ToHttpResult())
            .WithName("ListProducts")
            .WithSummary("Published products for the storefront, newest first, with search and category filter.")
            .WithValidation<ListProducts.Query>()
            .Produces<ListProducts.Response>()
            .AllowAnonymous();

        group.MapGet("/{slug}", static async (string slug, GetProductBySlug.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new GetProductBySlug.Query(slug), cancellationToken)).ToHttpResult())
            .WithName("GetProductBySlug")
            .WithSummary("A published product page.")
            .Produces<GetProductBySlug.Response>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AllowAnonymous();

        group.MapPost("/", static async (CreateProduct.Command command, CreateProduct.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(command, cancellationToken))
                    .ToHttpResult(created => TypedResults.Created($"/api/catalog/products/{created.Slug}", created)))
            .WithName("CreateProduct")
            .WithSummary("Create a draft product with its variants.")
            .WithValidation<CreateProduct.Command>()
            .Produces<CreateProduct.Response>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(CatalogPermissions.ProductsEdit);

        group.MapPost("/{id:guid}/publish", static async (Guid id, PublishProduct.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new PublishProduct.Command(id), cancellationToken)).ToHttpResult())
            .WithName("PublishProduct")
            .WithSummary("Make a draft product visible on the storefront.")
            .Produces<PublishProduct.Response>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(CatalogPermissions.ProductsPublish);
    }
}
