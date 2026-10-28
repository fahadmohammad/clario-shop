using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ClarioShop.SharedKernel.Domain;

namespace ClarioShop.Identity.Domain;

/// <summary>
/// A one-time sign-in code sent to a phone. Only a hash of the code is stored. A code works once, until it expires,
/// and only for a limited number of attempts; the newest code for a phone is the only one checked.
/// </summary>
internal sealed class OtpCode : Entity
{
    public const int Length = 6;

    private OtpCode()
    {
    }

    /// <summary>E.164.</summary>
    public string Phone { get; private set; } = string.Empty;

    public byte[] CodeHash { get; private set; } = [];

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Codes checked against this one so far, right or wrong.</summary>
    public int Attempts { get; private set; }

    public DateTimeOffset? ConsumedAt { get; private set; }

    /// <summary>A new code for the phone; <paramref name="code"/> is the plain code to send, never stored.</summary>
    public static OtpCode Issue(string phone, DateTimeOffset now, TimeSpan lifetime, out string code)
    {
        code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        var otp = new OtpCode
        {
            Id = Guid.CreateVersion7(),
            Phone = phone,
            CreatedAt = now,
            ExpiresAt = now + lifetime,
        };
        otp.CodeHash = otp.Hash(code);
        return otp;
    }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public bool Matches(string code) =>
        code.Length == Length && CryptographicOperations.FixedTimeEquals(Hash(code), CodeHash);

    /// <summary>Salted with the code's own id, so equal codes never have equal hashes.</summary>
    private byte[] Hash(string code) => SHA256.HashData(Encoding.UTF8.GetBytes($"{Id:N}:{code}"));
}
