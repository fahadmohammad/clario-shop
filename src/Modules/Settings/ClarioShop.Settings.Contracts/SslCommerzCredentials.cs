namespace ClarioShop.Settings.Contracts;

/// <summary>An SSLCommerz merchant account. <see cref="ToString"/> leaves out the password, so it cannot reach a log.</summary>
/// <param name="StoreId">The merchant's store id (SSLCommerz <c>store_id</c>).</param>
/// <param name="StorePassword">The API password (SSLCommerz <c>store_passwd</c>).</param>
/// <param name="UseSandbox">True for the sandbox (test) environment, false for live payments.</param>
public sealed record SslCommerzCredentials(string StoreId, string StorePassword, bool UseSandbox)
{
    public override string ToString() => $"SslCommerzCredentials {{ StoreId = {StoreId}, UseSandbox = {UseSandbox} }}";
}
