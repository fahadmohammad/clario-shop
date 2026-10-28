using ClarioShop.Identity.Domain;

namespace ClarioShop.Identity.Tests.Domain;

public sealed class OtpCodeTests
{
    private const string Phone = "+8801712345678";

    private static readonly DateTimeOffset Now = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(3);

    [Fact]
    public void Issue_Always_ReturnsSixDigitCode()
    {
        OtpCode.Issue(Phone, Now, Lifetime, out var code);

        Assert.Matches("^[0-9]{6}$", code);
    }

    [Fact]
    public void Issue_Always_StoresHashNotCode()
    {
        var otp = OtpCode.Issue(Phone, Now, Lifetime, out var code);

        Assert.Equal(32, otp.CodeHash.Length);
        Assert.DoesNotContain(code, Convert.ToHexString(otp.CodeHash), StringComparison.Ordinal);
    }

    [Fact]
    public void Matches_IssuedCode_ReturnsTrue()
    {
        var otp = OtpCode.Issue(Phone, Now, Lifetime, out var code);

        Assert.True(otp.Matches(code));
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1234567")]
    public void Matches_WrongLength_ReturnsFalse(string code)
    {
        var otp = OtpCode.Issue(Phone, Now, Lifetime, out _);

        Assert.False(otp.Matches(code));
    }

    [Fact]
    public void Matches_OtherCode_ReturnsFalse()
    {
        var otp = OtpCode.Issue(Phone, Now, Lifetime, out var code);
        var other = ((int.Parse(code, System.Globalization.CultureInfo.InvariantCulture) + 1) % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);

        Assert.False(otp.Matches(other));
    }

    [Fact]
    public void Issue_SameCodeTwice_HashesDiffer()
    {
        var first = OtpCode.Issue(Phone, Now, Lifetime, out _);
        var second = OtpCode.Issue(Phone, Now, Lifetime, out _);

        Assert.NotEqual(first.CodeHash, second.CodeHash);
    }

    [Fact]
    public void IsExpired_BeforeLifetimeEnds_ReturnsFalse()
    {
        var otp = OtpCode.Issue(Phone, Now, Lifetime, out _);

        Assert.False(otp.IsExpired(Now + Lifetime - TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void IsExpired_AtLifetimeEnd_ReturnsTrue()
    {
        var otp = OtpCode.Issue(Phone, Now, Lifetime, out _);

        Assert.True(otp.IsExpired(Now + Lifetime));
    }
}
