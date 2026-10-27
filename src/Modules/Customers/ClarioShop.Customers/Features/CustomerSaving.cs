using ClarioShop.Customers.Domain;
using ClarioShop.Customers.Features.Customers;
using ClarioShop.Customers.Persistence;

namespace ClarioShop.Customers.Features;

internal static class CustomerSaving
{
    /// <summary>Saves a changed customer; a concurrent change to the same customer is a conflict, not a lost update.</summary>
    public static async Task<Result<CustomerDetails>> SaveCustomerAsync(this CustomersDbContext db, Customer customer, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return CustomerDetails.From(customer);
        }
        catch (DbUpdateConcurrencyException)
        {
            return CustomerErrors.Concurrency;
        }
    }
}
