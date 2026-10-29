using ClarioShop.Catalog.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace ClarioShop.Catalog.Features.Images;

internal sealed class ImageEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/catalog/products/{id:guid}/images").WithTags("Catalog");

        // Bearer tokens, not cookies, authenticate staff, so a form post from another site carries no credentials.
        group.MapPost("/", static async (
                Guid id, IFormFile? file, [FromForm] string? altText, UploadProductImage.Handler handler, CancellationToken cancellationToken) =>
            {
                await using var content = file?.OpenReadStream() ?? Stream.Null;
                var command = new UploadProductImage.Command(id, content, file?.Length ?? 0, altText);
                return (await handler.HandleAsync(command, cancellationToken))
                    .ToHttpResult(image => TypedResults.Created(image.Url, image));
            })
            .WithName("UploadProductImage")
            .WithSummary("Add a photo (multipart form: file, optional altText) at the end of the gallery. JPEG, PNG, WebP or AVIF, up to 5 MB.")
            .DisableAntiforgery()
            .Produces<ImageDetails>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(CatalogPermissions.ProductsEdit);

        group.MapDelete("/{imageId:guid}", static async (Guid id, Guid imageId, RemoveProductImage.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new RemoveProductImage.Command(id, imageId), cancellationToken)).ToHttpResult())
            .WithName("RemoveProductImage")
            .WithSummary("Remove a photo; returns the gallery left. The next photo becomes the main one if this was it.")
            .Produces<IReadOnlyList<ImageDetails>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(CatalogPermissions.ProductsEdit);

        group.MapPut("/order", static async (Guid id, ReorderProductImages.Request order, ReorderProductImages.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new ReorderProductImages.Command(id, order), cancellationToken)).ToHttpResult())
            .WithName("ReorderProductImages")
            .WithSummary("Put the gallery in a new order, listing every image id once; the first becomes the main image.")
            .WithValidation<ReorderProductImages.Request>()
            .Produces<IReadOnlyList<ImageDetails>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(CatalogPermissions.ProductsEdit);

        group.MapPost("/{imageId:guid}/main", static async (Guid id, Guid imageId, SetMainProductImage.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new SetMainProductImage.Command(id, imageId), cancellationToken)).ToHttpResult())
            .WithName("SetMainProductImage")
            .WithSummary("Make a photo the main image; the others keep their order behind it.")
            .Produces<IReadOnlyList<ImageDetails>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(CatalogPermissions.ProductsEdit);

        app.MapGet("/api/catalog/images/{imageId:guid}", static async (
                Guid imageId, GetImageContent.Handler handler, HttpResponse response, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new GetImageContent.Query(imageId), cancellationToken)).ToHttpResult(image =>
                {
                    // An image id always names the same bytes (replacing a photo means a new image), so caches keep it for good.
                    response.Headers.CacheControl = "public, max-age=31536000, immutable";
                    response.Headers.XContentTypeOptions = "nosniff";
                    return TypedResults.Stream(image.Content, image.ContentType);
                }))
            .WithTags("Catalog")
            .WithName("GetProductImage")
            .WithSummary("A product photo's file. Ids are unguessable, so images of drafts can be previewed without a token.")
            .Produces(StatusCodes.Status200OK, typeof(Stream), "image/jpeg", "image/png", "image/webp", "image/avif")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AllowAnonymous();
    }
}
