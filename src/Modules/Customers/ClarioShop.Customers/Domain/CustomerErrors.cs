using ClarioShop.SharedKernel.Results;

namespace ClarioShop.Customers.Domain;

internal static class CustomerErrors
{
    public static readonly Error NotFound =
        Error.NotFound("customers.not_found", "The customer does not exist.");

    public static readonly Error AddressNotFound =
        Error.NotFound("customers.address_not_found", "The address does not exist for this customer.");

    public static readonly Error TooManyAddresses =
        Error.Validation("addresses", $"A customer can have at most {Customer.MaxAddresses} addresses.");

    public static readonly Error Concurrency =
        Error.Conflict("customers.concurrency", "The customer changed while saving; please try again.");

    public static Error InvalidPhone(string property) =>
        Error.Validation(property, "Enter a Bangladeshi mobile number, e.g. 01712-345678.");

    public static Error PhoneTaken(string phone) =>
        Error.Conflict("customers.phone_taken", $"A customer with phone {phone} already exists.");
}
