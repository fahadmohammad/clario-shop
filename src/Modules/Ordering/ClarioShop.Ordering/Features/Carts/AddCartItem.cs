using ClarioShop.Catalog.Contracts;
using ClarioShop.Ordering.Domain;
using ClarioShop.Ordering.Persistence;

namespace ClarioShop.Ordering.Features.Carts;

public static class AddCartItem
{
    /// <param name="Quantity">Units, or kg for items sold by weight. Added to the quantity already in the cart.</param>
    public sealed record Request(Guid VariantId, decimal Quantity);

    public sealed record Command(Guid CartId, Guid VariantId, decimal Quantity);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.VariantId).NotEmpty();
            RuleFor(request => request.Quantity).ValidCartQuantity();
        }
    }

    internal sealed class Handler(OrderingDbContext db, ICatalogQueries catalog, TimeProvider clock) : ICommandHandler<Command, CartDetails>
    {
        public async Task<Result<CartDetails>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            var cart = await db.FindCartAsync(command.CartId, cancellationToken);
            if (cart is null)
            {
                return OrderingErrors.CartNotFound;
            }

            var allowed = await CartItemRules.CheckAsync(catalog, command.VariantId, command.Quantity, cancellationToken);
            if (allowed.IsFailure)
            {
                return allowed.Error;
            }

            var added = cart.AddItem(command.VariantId, command.Quantity, clock.GetUtcNow());
            return added.IsFailure ? added.Error : await db.SaveCartAsync(cart, catalog, cancellationToken);
        }
    }
}
