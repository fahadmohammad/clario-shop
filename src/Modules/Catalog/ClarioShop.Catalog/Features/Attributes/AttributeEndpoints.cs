using ClarioShop.Catalog.Contracts;

namespace ClarioShop.Catalog.Features.Attributes;

internal sealed class AttributeEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/catalog/attributes").WithTags("Catalog");

        group.MapGet("/", static async (ListAttributes.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new ListAttributes.Query(), cancellationToken)).ToHttpResult())
            .WithName("ListAttributes")
            .WithSummary("All variant attributes by name, each with its values in order, for picking a variant's values.")
            .Produces<IReadOnlyList<AttributeItem>>()
            .RequirePermission(CatalogPermissions.ProductsView);

        group.MapPost("/", static async (CreateAttribute.Command command, CreateAttribute.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(command, cancellationToken))
                    .ToHttpResult(created => TypedResults.Created($"/api/catalog/attributes/{created.Id}", created)))
            .WithName("CreateAttribute")
            .WithSummary("Add an attribute such as Size, with its first values. Names are unique per shop, ignoring case.")
            .WithValidation<CreateAttribute.Command>()
            .Produces<AttributeItem>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(CatalogPermissions.AttributesEdit);

        group.MapPut("/{id:guid}", static async (Guid id, RenameAttribute.Request attribute, RenameAttribute.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new RenameAttribute.Command(id, attribute), cancellationToken)).ToHttpResult())
            .WithName("RenameAttribute")
            .WithSummary("Rename an attribute.")
            .WithValidation<RenameAttribute.Request>()
            .Produces<AttributeItem>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(CatalogPermissions.AttributesEdit);

        group.MapPost("/{id:guid}/values", static async (Guid id, AddAttributeValue.Request value, AddAttributeValue.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new AddAttributeValue.Command(id, value), cancellationToken))
                    .ToHttpResult(updated => TypedResults.Created($"/api/catalog/attributes/{id}", updated)))
            .WithName("AddAttributeValue")
            .WithSummary("Add a value after the attribute's existing ones. Values are unique per attribute, ignoring case.")
            .WithValidation<AddAttributeValue.Request>()
            .Produces<AttributeItem>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(CatalogPermissions.AttributesEdit);

        group.MapPut("/{id:guid}/values/{valueId:guid}", static async (
                Guid id, Guid valueId, RenameAttributeValue.Request value, RenameAttributeValue.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new RenameAttributeValue.Command(id, valueId, value), cancellationToken)).ToHttpResult())
            .WithName("RenameAttributeValue")
            .WithSummary("Rename a value. Variants that have it show the new spelling.")
            .WithValidation<RenameAttributeValue.Request>()
            .Produces<AttributeItem>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(CatalogPermissions.AttributesEdit);
    }
}
