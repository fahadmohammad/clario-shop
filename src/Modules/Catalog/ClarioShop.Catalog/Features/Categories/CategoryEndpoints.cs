using ClarioShop.Catalog.Contracts;

namespace ClarioShop.Catalog.Features.Categories;

internal sealed class CategoryEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/catalog/categories").WithTags("Catalog");

        group.MapGet("/", static async (ListCategories.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new ListCategories.Query(), cancellationToken)).ToHttpResult())
            .WithName("ListCategories")
            .WithSummary("All categories, for storefront navigation.")
            .Produces<IReadOnlyList<ListCategories.CategoryItem>>()
            .AllowAnonymous();

        group.MapPost("/", static async (CreateCategory.Command command, CreateCategory.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(command, cancellationToken))
                    .ToHttpResult(created => TypedResults.Created($"/api/catalog/categories/{created.Slug}", created)))
            .WithName("CreateCategory")
            .WithValidation<CreateCategory.Command>()
            .Produces<CreateCategory.Response>(StatusCodes.Status201Created)
            .RequirePermission(CatalogPermissions.CategoriesEdit);
    }
}
