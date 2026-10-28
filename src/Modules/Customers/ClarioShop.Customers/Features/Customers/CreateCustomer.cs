using ClarioShop.Customers.Contracts.Events;
using ClarioShop.Customers.Domain;
using ClarioShop.Customers.Persistence;
using Npgsql;

namespace ClarioShop.Customers.Features.Customers;

public static class CreateCustomer
{
    public sealed record Command(string Phone, string? Name, string? Email);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.Phone).Must(phone => PhoneNumber.TryNormalize(phone, out _))
                .WithMessage("Enter a Bangladeshi mobile number, e.g. 01712-345678.");
            RuleFor(command => command.Name).MaximumLength(200);
            RuleFor(command => command.Email).MaximumLength(254).EmailAddress()
                .When(command => !string.IsNullOrWhiteSpace(command.Email));
        }
    }

    internal sealed class Handler(CustomersDbContext db, TimeProvider clock) : ICommandHandler<Command, CustomerDetails>
    {
        public async Task<Result<CustomerDetails>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            if (!PhoneNumber.TryNormalize(command.Phone, out var phone))
            {
                return CustomerErrors.InvalidPhone(nameof(Command.Phone));
            }

            if (await db.Customers.AnyAsync(c => c.Phone == phone, cancellationToken))
            {
                return CustomerErrors.PhoneTaken(phone);
            }

            var now = clock.GetUtcNow();
            var customer = Customer.Create(phone, command.Name, command.Email, now);
            db.Customers.Add(customer);
            db.Publish(new CustomerCreated(Guid.CreateVersion7(), now, customer.Id, customer.Phone, customer.Name));
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                return CustomerErrors.PhoneTaken(phone);
            }

            return CustomerDetails.From(customer);
        }
    }
}
