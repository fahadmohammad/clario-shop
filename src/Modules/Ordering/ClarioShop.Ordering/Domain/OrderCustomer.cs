namespace ClarioShop.Ordering.Domain;

/// <param name="Phone">E.164, as the Customers module normalized it.</param>
internal sealed record OrderCustomer(Guid CustomerId, string Phone, string Name);
