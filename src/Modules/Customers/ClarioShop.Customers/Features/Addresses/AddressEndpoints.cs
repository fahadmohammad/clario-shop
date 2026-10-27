using ClarioShop.Customers.Contracts;
using ClarioShop.Customers.Features.Customers;

namespace ClarioShop.Customers.Features.Addresses;

internal sealed class AddressEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/customers/{customerId:guid}/addresses").WithTags("Customers");

        group.MapPost("/", static async (Guid customerId, AddressInput address, AddAddress.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new AddAddress.Command(customerId, address), cancellationToken))
                    .ToHttpResult(customer => TypedResults.Created($"/api/customers/{customer.Id}", customer)))
            .WithName("AddCustomerAddress")
            .WithSummary("Add a delivery address. The first address, or one sent with makeDefault, becomes the default.")
            .WithValidation<AddressInput>()
            .Produces<CustomerDetails>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(CustomersPermissions.Edit);

        group.MapPut("/{addressId:guid}", static async (Guid customerId, Guid addressId, AddressInput address, UpdateAddress.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new UpdateAddress.Command(customerId, addressId, address), cancellationToken)).ToHttpResult())
            .WithName("UpdateCustomerAddress")
            .WithSummary("Replace an address. makeDefault true makes it the default; false leaves the default as it is.")
            .WithValidation<AddressInput>()
            .Produces<CustomerDetails>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(CustomersPermissions.Edit);

        group.MapDelete("/{addressId:guid}", static async (Guid customerId, Guid addressId, RemoveAddress.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new RemoveAddress.Command(customerId, addressId), cancellationToken)).ToHttpResult())
            .WithName("RemoveCustomerAddress")
            .WithSummary("Remove an address. When it was the default, the oldest remaining address becomes the default.")
            .Produces<CustomerDetails>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(CustomersPermissions.Edit);
    }
}
