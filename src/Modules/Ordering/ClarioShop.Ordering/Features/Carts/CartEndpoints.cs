namespace ClarioShop.Ordering.Features.Carts;

/// <summary>Anonymous carts: whoever holds the cart id can use it, so the storefront keeps it like a session.</summary>
internal sealed class CartEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ordering/carts").WithTags("Ordering");

        group.MapPost("/", static async (CreateCart.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new CreateCart.Command(), cancellationToken))
                    .ToHttpResult(cart => TypedResults.Created($"/api/ordering/carts/{cart.Id}", cart)))
            .WithName("CreateCart")
            .WithSummary("Start an empty cart; the storefront keeps its id for the shopper.")
            .Produces<CartDetails>(StatusCodes.Status201Created)
            .AllowAnonymous();

        group.MapGet("/{cartId:guid}", static async (Guid cartId, GetCart.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new GetCart.Query(cartId), cancellationToken)).ToHttpResult())
            .WithName("GetCart")
            .WithSummary("A cart at current catalog prices; items no longer for sale are flagged and left out of the subtotal.")
            .Produces<CartDetails>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AllowAnonymous();

        group.MapPost("/{cartId:guid}/items", static async (Guid cartId, AddCartItem.Request request, AddCartItem.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new AddCartItem.Command(cartId, request.VariantId, request.Quantity), cancellationToken)).ToHttpResult())
            .WithName("AddCartItem")
            .WithSummary("Add a variant that is for sale; adding one already in the cart increases its quantity.")
            .WithValidation<AddCartItem.Request>()
            .Produces<CartDetails>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .AllowAnonymous();

        group.MapPut("/{cartId:guid}/items/{variantId:guid}", static async (Guid cartId, Guid variantId, UpdateCartItem.Request request, UpdateCartItem.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new UpdateCartItem.Command(cartId, variantId, request.Quantity), cancellationToken)).ToHttpResult())
            .WithName("UpdateCartItem")
            .WithSummary("Set the quantity of an item in the cart.")
            .WithValidation<UpdateCartItem.Request>()
            .Produces<CartDetails>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .AllowAnonymous();

        group.MapDelete("/{cartId:guid}/items/{variantId:guid}", static async (Guid cartId, Guid variantId, RemoveCartItem.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new RemoveCartItem.Command(cartId, variantId), cancellationToken)).ToHttpResult())
            .WithName("RemoveCartItem")
            .WithSummary("Take an item out of the cart.")
            .Produces<CartDetails>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .AllowAnonymous();
    }
}
