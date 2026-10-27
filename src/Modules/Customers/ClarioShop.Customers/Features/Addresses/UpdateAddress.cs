using ClarioShop.Customers.Domain;
using ClarioShop.Customers.Features.Customers;
using ClarioShop.Customers.Persistence;

namespace ClarioShop.Customers.Features.Addresses;

public static class UpdateAddress
{
    public sealed record Command(Guid CustomerId, Guid AddressId, AddressInput Address);

    internal sealed class Handler(CustomersDbContext db, TimeProvider clock) : ICommandHandler<Command, CustomerDetails>
    {
        public async Task<Result<CustomerDetails>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            var customer = await db.Customers.Include(c => c.Addresses).FirstOrDefaultAsync(c => c.Id == command.CustomerId, cancellationToken);
            if (customer is null)
            {
                return CustomerErrors.NotFound;
            }

            var updated = customer.UpdateAddress(command.AddressId, command.Address.ToDetails(), command.Address.MakeDefault, clock.GetUtcNow());
            if (updated.IsFailure)
            {
                return updated.Error;
            }

            return await db.SaveCustomerAsync(customer, cancellationToken);
        }
    }
}
