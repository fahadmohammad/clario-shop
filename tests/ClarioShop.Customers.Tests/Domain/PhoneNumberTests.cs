using ClarioShop.Customers.Domain;

namespace ClarioShop.Customers.Tests.Domain;

public sealed class PhoneNumberTests
{
    [Theory]
    [InlineData("01712-345678")]
    [InlineData("+8801712345678")]
    [InlineData("8801712345678")]
    [InlineData("008801712345678")]
    [InlineData("01712345678")]
    [InlineData(" 017 1234 5678 ")]
    [InlineData("+880 1712-345678")]
    [InlineData("(017) 12.345.678")]
    [InlineData("1712345678")]
    [InlineData("০১৭১২-৩৪৫৬৭৮")]
    public void TryNormalize_SameNumberInAnyFormat_ReturnsOneE164Number(string input)
    {
        var valid = PhoneNumber.TryNormalize(input, out var normalized);

        Assert.True(valid);
        Assert.Equal("+8801712345678", normalized);
    }

    [Theory]
    [InlineData("01312345678", "+8801312345678")]
    [InlineData("01912345678", "+8801912345678")]
    public void TryNormalize_EveryOperatorPrefix_IsAccepted(string input, string expected)
    {
        Assert.True(PhoneNumber.TryNormalize(input, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0171234567")]
    [InlineData("017123456789")]
    [InlineData("01212345678")]
    [InlineData("02912345678")]
    [InlineData("+9101712345678")]
    [InlineData("+01712345678")]
    [InlineData("01712a45678")]
    [InlineData("017+12345678")]
    [InlineData("01712345678901234567890123456789012")]
    public void TryNormalize_NotABangladeshiMobileNumber_ReturnsFalse(string? input)
    {
        Assert.False(PhoneNumber.TryNormalize(input, out _));
    }

    [Theory]
    [InlineData("01712", "1712")]
    [InlineData("+8801712", "1712")]
    [InlineData("8801712", "1712")]
    [InlineData("345-678", "345678")]
    [InlineData("০১৭১২", "1712")]
    public void SearchDigits_FragmentInAnyFormat_StripsCountryCodeAndTrunkZero(string input, string expected)
    {
        Assert.Equal(expected, PhoneNumber.SearchDigits(input));
    }
}
