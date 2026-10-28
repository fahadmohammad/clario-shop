namespace ClarioShop.Identity.Services;

/// <summary>Token server settings, section "Auth".</summary>
internal sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>Absolute URI naming this deployment's token server; one issuer for every tenant.</summary>
    public string Issuer { get; set; } = "https://clarioshop.local/";

    public string Audience { get; set; } = "clarioshop-api";

    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);

    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(14);

    /// <summary>How long a refresh token that was just exchanged still works, for concurrent requests from one app.</summary>
    public TimeSpan RefreshTokenReuseLeeway { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Base64 PKCS#12 certificate with its private key, from secrets. Required outside Development and Testing.</summary>
    public string? SigningCertificate { get; set; }

    /// <summary>Base64 PKCS#12 certificate with its private key, from secrets. Required outside Development and Testing.</summary>
    public string? EncryptionCertificate { get; set; }

    public string? CertificatePassword { get; set; }
}
