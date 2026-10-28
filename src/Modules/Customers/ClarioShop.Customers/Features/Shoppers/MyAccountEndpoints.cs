using System.Security.Claims;
using ClarioShop.Customers.Features.Addresses;
using ClarioShop.Customers.Features.Customers;
using ClarioShop.SharedKernel.Tenancy;

namespace ClarioShop.Customers.Features.Shoppers;

/// <summary>
/// The signed-in shopper's own profile and addresses. The customer always comes from the token, so a shopper can only
/// ever read or change their own record; another customer's address id is simply not found.
/// </summary>
internal sealed class MyAccountEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/customers/me").WithTags("My account").RequireShopper();

        group.MapGet("/", static async (ClaimsPrincipal user, ITenantContext tenant, GetCustomer.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new GetCustomer.Query(Me(user, tenant)), cancellationToken)).ToHttpResult())
            .WithName("GetMyProfile")
            .WithSummary("The signed-in shopper's profile and saved addresses, default first.")
            .Produces<CustomerDetails>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/", static async (UpdateCustomer.Request request, ClaimsPrincipal user, ITenantContext tenant, UpdateCustomer.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new UpdateCustomer.Command(Me(user, tenant), request.Name, request.Email), cancellationToken)).ToHttpResult())
            .WithName("UpdateMyProfile")
            .WithSummary("Change the signed-in shopper's name and email. The phone number is the sign-in and cannot change.")
            .WithValidation<UpdateCustomer.Request>()
            .Produces<CustomerDetails>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/addresses", static async (AddressInput address, ClaimsPrincipal user, ITenantContext tenant, AddAddress.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new AddAddress.Command(Me(user, tenant), address), cancellationToken))
                    .ToHttpResult(customer => TypedResults.Created("/api/customers/me", customer)))
            .WithName("AddMyAddress")
            .WithSummary("Save a delivery address. The first address, or one sent with makeDefault, becomes the default.")
            .WithValidation<AddressInput>()
            .Produces<CustomerDetails>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/addresses/{addressId:guid}", static async (Guid addressId, AddressInput address, ClaimsPrincipal user, ITenantContext tenant, UpdateAddress.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new UpdateAddress.Command(Me(user, tenant), addressId, address), cancellationToken)).ToHttpResult())
            .WithName("UpdateMyAddress")
            .WithSummary("Replace a saved address. makeDefault true makes it the default; false leaves the default as it is.")
            .WithValidation<AddressInput>()
            .Produces<CustomerDetails>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/addresses/{addressId:guid}", static async (Guid addressId, ClaimsPrincipal user, ITenantContext tenant, RemoveAddress.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new RemoveAddress.Command(Me(user, tenant), addressId), cancellationToken)).ToHttpResult())
            .WithName("RemoveMyAddress")
            .WithSummary("Remove a saved address. When it was the default, the oldest remaining address becomes the default.")
            .Produces<CustomerDetails>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    /// <summary>The shopper's customer id; the group's shopper policy guarantees there is one.</summary>
    private static Guid Me(ClaimsPrincipal user, ITenantContext tenant) =>
        user.ShopperCustomerId(tenant) ?? throw new InvalidOperationException("Shopper endpoint reached without a shopper.");
}
