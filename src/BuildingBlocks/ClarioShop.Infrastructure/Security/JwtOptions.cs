namespace ClarioShop.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Auth";

    public string Issuer { get; set; } = "clarioshop";

    public string Audience { get; set; } = "clarioshop-api";

    /// <summary>HMAC key, at least 32 bytes. Comes from secrets, never from source.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);
}
