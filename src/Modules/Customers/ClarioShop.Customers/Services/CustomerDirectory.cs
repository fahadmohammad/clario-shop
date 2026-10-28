using ClarioShop.Customers.Contracts;
using ClarioShop.Customers.Contracts.Events;
using ClarioShop.Customers.Domain;
using ClarioShop.Customers.Persistence;
using Npgsql;

namespace ClarioShop.Customers.Services;

internal sealed class CustomerDirectory(CustomersDbContext db, TimeProvider clock) : ICustomerDirectory
{
    /// <summary>Attempts before giving up when concurrent callers keep changing the same customer.</summary>
    private const int MaxAttempts = 5;

    public async Task<Result<CustomerInfo>> FindOrCreateByPhoneAsync(string phone, string? name, CancellationToken cancellationToken)
    {
        if (!PhoneNumber.TryNormalize(phone, out var normalized))
        {
            return CustomerErrors.InvalidPhone(nameof(phone));
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await FindOrCreateAsync(normalized, name, cancellationToken);
            }
            catch (DbUpdateException exception) when (attempt < MaxAttempts && IsRace(exception))
            {
                // Another caller created this customer, or filled in its name, first; read it again.
                db.ChangeTracker.Clear();
            }
        }
    }

    public async Task<CustomerInfo?> GetAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.AsNoTracking()
            .Include(c => c.Addresses)
            .FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);
        return customer?.ToInfo();
    }

    public string? NormalizePhone(string phone) => PhoneNumber.TryNormalize(phone, out var normalized) ? normalized : null;

    private async Task<CustomerInfo> FindOrCreateAsync(string phone, string? name, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var customer = await db.Customers.Include(c => c.Addresses).FirstOrDefaultAsync(c => c.Phone == phone, cancellationToken);
        if (customer is null)
        {
            customer = Customer.Create(phone, name, email: null, now);
            db.Customers.Add(customer);
            db.Publish(new CustomerCreated(Guid.CreateVersion7(), now, customer.Id, customer.Phone, customer.Name));
            await db.SaveChangesAsync(cancellationToken);
        }
        else if (customer.FillMissingName(name, now))
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return customer.ToInfo();
    }

    private static bool IsRace(DbUpdateException exception) =>
        exception is DbUpdateConcurrencyException
        || exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
