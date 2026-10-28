using ClarioShop.SharedKernel.Results;

namespace ClarioShop.Customers.Contracts;

/// <summary>
/// Synchronous customer lookup for checkout, POS and sign-in. One customer per Bangladeshi mobile number,
/// whatever format the number is written in.
/// </summary>
public interface ICustomerDirectory
{
    /// <summary>
    /// Returns the customer with this phone number, creating it (and publishing <c>CustomerCreated</c>) when
    /// there is none. Safe to call concurrently for the same number: every caller gets the same customer.
    /// An existing customer's name is only filled in when it has none. Fails with a validation error when
    /// the number is not a Bangladeshi mobile number.
    /// </summary>
    Task<Result<CustomerInfo>> FindOrCreateByPhoneAsync(string phone, string? name, CancellationToken cancellationToken);

    /// <summary>The customer with its addresses, default first; null when it does not exist.</summary>
    Task<CustomerInfo?> GetAsync(Guid customerId, CancellationToken cancellationToken);

    /// <summary>
    /// The number in E.164 (<c>+8801712345678</c>), the form customers are matched by; null when it is not a
    /// Bangladeshi mobile number. Creates nothing.
    /// </summary>
    string? NormalizePhone(string phone);
}

/// <param name="Phone">E.164, e.g. <c>+8801712345678</c>.</param>
public sealed record CustomerInfo(
    Guid CustomerId,
    string Phone,
    string? Name,
    string? Email,
    IReadOnlyList<CustomerAddressInfo> Addresses);

public sealed record CustomerAddressInfo(
    Guid AddressId,
    string? Label,
    string RecipientName,
    string RecipientPhone,
    string Line1,
    string? Line2,
    string Area,
    string City,
    string? PostalCode,
    bool IsDefault);
