using ClarioShop.Customers.Contracts;

namespace ClarioShop.Customers.Features.Customers;

internal sealed class CustomerEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/customers").WithTags("Customers");

        group.MapGet("/", static async ([AsParameters] SearchCustomers.Query query, SearchCustomers.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(query, cancellationToken)).ToHttpResult())
            .WithName("SearchCustomers")
            .WithSummary("Customers, newest first, searchable by name, email or any part of the phone number.")
            .WithValidation<SearchCustomers.Query>()
            .Produces<SearchCustomers.Response>()
            .RequirePermission(CustomersPermissions.View);

        group.MapGet("/{id:guid}", static async (Guid id, GetCustomer.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new GetCustomer.Query(id), cancellationToken)).ToHttpResult())
            .WithName("GetCustomer")
            .WithSummary("A customer with addresses, default first.")
            .Produces<CustomerDetails>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequirePermission(CustomersPermissions.View);

        group.MapPost("/", static async (CreateCustomer.Command command, CreateCustomer.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(command, cancellationToken))
                    .ToHttpResult(created => TypedResults.Created($"/api/customers/{created.Id}", created)))
            .WithName("CreateCustomer")
            .WithSummary("Register a customer by mobile number, e.g. at the till.")
            .WithValidation<CreateCustomer.Command>()
            .Produces<CustomerDetails>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(CustomersPermissions.Edit);

        group.MapPut("/{id:guid}", static async (Guid id, UpdateCustomer.Request request, UpdateCustomer.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new UpdateCustomer.Command(id, request.Name, request.Email), cancellationToken)).ToHttpResult())
            .WithName("UpdateCustomer")
            .WithSummary("Change a customer's name and email. The phone number is the customer's identity and cannot change.")
            .WithValidation<UpdateCustomer.Request>()
            .Produces<CustomerDetails>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(CustomersPermissions.Edit);
    }
}
