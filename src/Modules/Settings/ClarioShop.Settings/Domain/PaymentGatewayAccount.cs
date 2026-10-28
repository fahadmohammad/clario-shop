using ClarioShop.SharedKernel.Domain;

namespace ClarioShop.Settings.Domain;

/// <summary>
/// The store's merchant account with one payment gateway, so its customers' money goes straight to the store. One
/// per gateway. The secret is stored only in its Data Protection form (see <c>GatewaySecretProtector</c>).
/// </summary>
internal sealed class PaymentGatewayAccount : Entity
{
    private PaymentGatewayAccount()
    {
    }

    public PaymentGateway Gateway { get; private set; }

    /// <summary>The merchant's id with the gateway, e.g. the SSLCommerz store id.</summary>
    public string MerchantId { get; private set; } = string.Empty;

    /// <summary>The API password or key, protected with the Data Protection API for this tenant.</summary>
    public string ProtectedSecret { get; private set; } = string.Empty;

    /// <summary>True for the gateway's test environment.</summary>
    public bool UseSandbox { get; private set; }

    /// <summary>False keeps the account but stops taking payments through it.</summary>
    public bool IsEnabled { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Concurrency token (PostgreSQL xmin).</summary>
    public uint RowVersion { get; private set; }

    public static PaymentGatewayAccount Create(PaymentGateway gateway, string merchantId, string protectedSecret, bool useSandbox, bool isEnabled, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        Gateway = gateway,
        MerchantId = merchantId.Trim(),
        ProtectedSecret = protectedSecret,
        UseSandbox = useSandbox,
        IsEnabled = isEnabled,
        CreatedAt = now,
        UpdatedAt = now,
    };

    /// <param name="protectedSecret">A new protected secret, or null to keep the current one.</param>
    public void Update(string merchantId, string? protectedSecret, bool useSandbox, bool isEnabled, DateTimeOffset now)
    {
        MerchantId = merchantId.Trim();
        ProtectedSecret = protectedSecret ?? ProtectedSecret;
        UseSandbox = useSandbox;
        IsEnabled = isEnabled;
        UpdatedAt = now;
    }
}
