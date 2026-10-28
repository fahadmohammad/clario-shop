using ClarioShop.Customers.Domain;

namespace ClarioShop.Customers.Features.Addresses;

/// <param name="RecipientPhone">Any Bangladeshi mobile format; stored as E.164.</param>
/// <param name="MakeDefault">True makes this the default address; false leaves the default unchanged.</param>
public sealed record AddressInput(
    string? Label,
    string RecipientName,
    string RecipientPhone,
    string Line1,
    string? Line2,
    string Area,
    string City,
    string? PostalCode,
    bool MakeDefault = false)
{
    internal AddressDetails ToDetails()
    {
        PhoneNumber.TryNormalize(RecipientPhone, out var phone);
        return new AddressDetails(Label, RecipientName, phone, Line1, Line2, Area, City, PostalCode);
    }
}

internal sealed class AddressInputValidator : AbstractValidator<AddressInput>
{
    public AddressInputValidator()
    {
        RuleFor(input => input.Label).MaximumLength(50);
        RuleFor(input => input.RecipientName).NotEmpty().MaximumLength(200);
        RuleFor(input => input.RecipientPhone).Must(phone => PhoneNumber.TryNormalize(phone, out _))
            .WithMessage("Enter a Bangladeshi mobile number, e.g. 01712-345678.");
        RuleFor(input => input.Line1).NotEmpty().MaximumLength(300);
        RuleFor(input => input.Line2).MaximumLength(300);
        RuleFor(input => input.Area).NotEmpty().MaximumLength(100);
        RuleFor(input => input.City).NotEmpty().MaximumLength(100);
        RuleFor(input => input.PostalCode).Matches("^[0-9]{4}$").WithMessage("Postal codes have four digits.")
            .When(input => !string.IsNullOrWhiteSpace(input.PostalCode));
    }
}
