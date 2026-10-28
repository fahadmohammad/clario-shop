using ClarioShop.Ordering.Features.Checkout;

namespace ClarioShop.Ordering.Tests.Features;

public sealed class CheckoutValidatorTests
{
    private static readonly Checkout.AddressInput Typed = new("House 12, Road 5", null, "Dhanmondi", "Dhaka", "1205");

    private readonly Checkout.Validator validator = new();

    [Fact]
    public void Validate_TypedAddressOnly_IsValid()
    {
        var result = validator.Validate(Request(Typed, savedAddressId: null));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_SavedAddressOnlyWithoutPhoneOrName_IsValid()
    {
        var result = validator.Validate(Request(address: null, Guid.CreateVersion7()) with { Phone = null, Name = null });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_NeitherAddress_IsInvalid()
    {
        var result = validator.Validate(Request(address: null, savedAddressId: null));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(Checkout.Request.Address));
    }

    [Fact]
    public void Validate_BothAddresses_IsInvalid()
    {
        var result = validator.Validate(Request(Typed, Guid.CreateVersion7()));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(Checkout.Request.Address));
    }

    [Fact]
    public void Validate_TypedAddressMissingArea_IsInvalid()
    {
        var result = validator.Validate(Request(Typed with { Area = "" }, savedAddressId: null));

        Assert.Contains(result.Errors, error => error.PropertyName.EndsWith(nameof(Checkout.AddressInput.Area), StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_EmptyNameSent_IsInvalid()
    {
        var result = validator.Validate(Request(Typed, savedAddressId: null) with { Name = " " });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(Checkout.Request.Name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("CashOnDelivery")]
    [InlineData("sslcommerz")]
    public void Validate_KnownPaymentMethod_IsValid(string? paymentMethod)
    {
        var result = validator.Validate(Request(Typed, savedAddressId: null) with { PaymentMethod = paymentMethod });

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("Bitcoin")]
    [InlineData("1")]
    [InlineData("")]
    public void Validate_UnknownPaymentMethod_IsInvalid(string paymentMethod)
    {
        var result = validator.Validate(Request(Typed, savedAddressId: null) with { PaymentMethod = paymentMethod });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(Checkout.Request.PaymentMethod));
    }

    private static Checkout.Request Request(Checkout.AddressInput? address, Guid? savedAddressId) =>
        new(Guid.CreateVersion7(), "01712-345678", "Rahim Uddin", address, savedAddressId, DeliveryZoneId: null, Note: null);
}
