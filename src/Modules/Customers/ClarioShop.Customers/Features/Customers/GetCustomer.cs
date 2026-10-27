using ClarioShop.Customers.Domain;
using ClarioShop.Customers.Persistence;

namespace ClarioShop.Customers.Features.Customers;

public static class GetCustomer
{
    public sealed record Query(Guid Id);

    internal sealed class Handler(CustomersDbContext db) : ICommandHandler<Query, CustomerDetails>
    {
        public async Task<Result<CustomerDetails>> HandleAsync(Query query, CancellationToken cancellationToken)
        {
            var customer = await db.Customers.AsNoTracking()
                .Include(c => c.Addresses)
                .FirstOrDefaultAsync(c => c.Id == query.Id, cancellationToken);
            return customer is null ? CustomerErrors.NotFound : CustomerDetails.From(customer);
        }
    }
}
