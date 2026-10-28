using ClarioShop.Customers.Domain;
using ClarioShop.Customers.Persistence;

namespace ClarioShop.Customers.Features.Customers;

/// <summary>Changes name and email. The phone number identifies the customer and is not editable.</summary>
public static class UpdateCustomer
{
    public sealed record Request(string? Name, string? Email);

    public sealed record Command(Guid Id, string? Name, string? Email);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Name).MaximumLength(200);
            RuleFor(request => request.Email).MaximumLength(254).EmailAddress()
                .When(request => !string.IsNullOrWhiteSpace(request.Email));
        }
    }

    internal sealed class Handler(CustomersDbContext db, TimeProvider clock) : ICommandHandler<Command, CustomerDetails>
    {
        public async Task<Result<CustomerDetails>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            var customer = await db.Customers.Include(c => c.Addresses).FirstOrDefaultAsync(c => c.Id == command.Id, cancellationToken);
            if (customer is null)
            {
                return CustomerErrors.NotFound;
            }

            customer.UpdateProfile(command.Name, command.Email, clock.GetUtcNow());
            return await db.SaveCustomerAsync(customer, cancellationToken);
        }
    }
}
