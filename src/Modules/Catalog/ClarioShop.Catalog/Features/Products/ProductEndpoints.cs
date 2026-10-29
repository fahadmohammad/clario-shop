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

        group.MapGet("/{id:guid}", static async (Guid id, GetProduct.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new GetProduct.Query(id), cancellationToken)).ToHttpResult())
            .WithName("GetProduct")
            .WithSummary("Any product by id, drafts and archived ones included, with every variant.")
            .Produces<ProductDetails>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequirePermission(CatalogPermissions.ProductsView);

        group.MapPut("/{id:guid}", static async (Guid id, UpdateProduct.Request product, UpdateProduct.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new UpdateProduct.Command(id, product), cancellationToken)).ToHttpResult())
            .WithName("UpdateProduct")
            .WithSummary("Replace a product's name, description, category and brand. The slug stays as created.")
            .WithValidation<UpdateProduct.Request>()
            .Produces<ProductDetails>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(CatalogPermissions.ProductsEdit);

        group.MapPost("/{id:guid}/archive", static async (Guid id, ArchiveProduct.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new ArchiveProduct.Command(id), cancellationToken)).ToHttpResult())
            .WithName("ArchiveProduct")
            .WithSummary("Take a product off the storefront for good. Its variants stop being sellable; history is kept.")
            .Produces<ProductDetails>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(CatalogPermissions.ProductsPublish);

        group.MapPost("/{id:guid}/variants", static async (Guid id, VariantInput variant, AddVariant.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new AddVariant.Command(id, variant), cancellationToken))
                    .ToHttpResult(added => TypedResults.Created($"/api/catalog/products/{id}", added)))
            .WithName("AddVariant")
            .WithSummary("Add a variant to an existing product. Inventory starts tracking it with zero stock.")
            .WithValidation<VariantInput>()
            .Produces<VariantDetails>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(CatalogPermissions.ProductsEdit);

        group.MapPut("/{id:guid}/variants/{variantId:guid}", static async (
                Guid id, Guid variantId, UpdateVariant.Request variant, UpdateVariant.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new UpdateVariant.Command(id, variantId, variant), cancellationToken)).ToHttpResult())
            .WithName("UpdateVariant")
            .WithSummary("Rename a variant, replace its barcodes, or take it off sale. SKU and sold-by-weight never change.")
            .WithValidation<UpdateVariant.Request>()
            .Produces<VariantDetails>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(CatalogPermissions.ProductsEdit);

        group.MapPut("/{id:guid}/variants/{variantId:guid}/price", static async (
                Guid id, Guid variantId, ChangeVariantPrice.Request prices, ChangeVariantPrice.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new ChangeVariantPrice.Command(id, variantId, prices), cancellationToken)).ToHttpResult())
            .WithName("ChangeVariantPrice")
            .WithSummary("Set a variant's price and compare-at price. Audited and published as ProductPriceChanged; placed orders keep their price.")
            .WithValidation<ChangeVariantPrice.Request>()
            .Produces<VariantDetails>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(CatalogPermissions.ProductsEdit);
    }
}
