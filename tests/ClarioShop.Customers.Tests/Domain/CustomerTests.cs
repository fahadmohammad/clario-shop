using ClarioShop.Customers.Domain;

namespace ClarioShop.Customers.Tests.Domain;

public sealed class CustomerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AddAddress_First_BecomesDefault()
    {
        var customer = NewCustomer();

        var address = customer.AddAddress(Details("Home"), makeDefault: false, Now).Value;

        Assert.True(address.IsDefault);
    }

    [Fact]
    public void AddAddress_SecondWithoutMakeDefault_KeepsExistingDefault()
    {
        var customer = NewCustomer();
        var home = customer.AddAddress(Details("Home"), makeDefault: false, Now).Value;

        var office = customer.AddAddress(Details("Office"), makeDefault: false, Now).Value;

        Assert.True(home.IsDefault);
        Assert.False(office.IsDefault);
    }

    [Fact]
    public void AddAddress_WithMakeDefault_MovesDefault()
    {
        var customer = NewCustomer();
        var home = customer.AddAddress(Details("Home"), makeDefault: false, Now).Value;

        var office = customer.AddAddress(Details("Office"), makeDefault: true, Now).Value;

        Assert.False(home.IsDefault);
        Assert.True(office.IsDefault);
        Assert.Single(customer.Addresses, a => a.IsDefault);
    }

    [Fact]
    public void AddAddress_OverLimit_Fails()
    {
        var customer = NewCustomer();
        for (var i = 0; i < Customer.MaxAddresses; i++)
        {
            customer.AddAddress(Details($"Address {i}"), makeDefault: false, Now);
        }

        var result = customer.AddAddress(Details("One too many"), makeDefault: false, Now);

        Assert.Equal(CustomerErrors.TooManyAddresses, result.Error);
        Assert.Equal(Customer.MaxAddresses, customer.Addresses.Count);
    }

    [Fact]
    public void UpdateAddress_WithoutMakeDefault_DoesNotUnsetDefault()
    {
        var customer = NewCustomer();
        var home = customer.AddAddress(Details("Home"), makeDefault: false, Now).Value;

        customer.UpdateAddress(home.Id, Details("Home, new flat"), makeDefault: false, Now);

        Assert.True(home.IsDefault);
        Assert.Equal("Home, new flat", home.Label);
    }

    [Fact]
    public void UpdateAddress_UnknownAddress_ReturnsNotFound()
    {
        var customer = NewCustomer();

        var result = customer.UpdateAddress(Guid.CreateVersion7(), Details("Home"), makeDefault: false, Now);

        Assert.Equal(CustomerErrors.AddressNotFound, result.Error);
    }

    [Fact]
    public void RemoveAddress_Default_PromotesOldestRemaining()
    {
        var customer = NewCustomer();
        var home = customer.AddAddress(Details("Home"), makeDefault: false, Now).Value;
        var office = customer.AddAddress(Details("Office"), makeDefault: false, Now.AddMinutes(1)).Value;
        customer.AddAddress(Details("Parents"), makeDefault: false, Now.AddMinutes(2));

        customer.RemoveAddress(home.Id, Now.AddMinutes(3));

        Assert.Equal(office, Assert.Single(customer.Addresses, a => a.IsDefault));
    }

    [Fact]
    public void FillMissingName_CustomerHasName_KeepsIt()
    {
        var customer = Customer.Create("+8801712345678", "Rahim Uddin", null, Now);

        var changed = customer.FillMissingName("Someone Else", Now);

        Assert.False(changed);
        Assert.Equal("Rahim Uddin", customer.Name);
    }

    [Fact]
    public void FillMissingName_CustomerHasNoName_SetsIt()
    {
        var customer = Customer.Create("+8801712345678", "  ", null, Now);

        var changed = customer.FillMissingName(" Karima Begum ", Now);

        Assert.True(changed);
        Assert.Equal("Karima Begum", customer.Name);
    }

    [Fact]
    public void Create_Email_IsTrimmedAndLowercased()
    {
        var customer = Customer.Create("+8801712345678", null, " Rahim@Example.COM ", Now);

        Assert.Equal("rahim@example.com", customer.Email);
    }

    private static Customer NewCustomer() => Customer.Create("+8801712345678", "Rahim Uddin", null, Now);

    private static AddressDetails Details(string label) =>
        new(label, "Rahim Uddin", "+8801712345678", "House 12, Road 5", null, "Dhanmondi", "Dhaka", "1205");
}
