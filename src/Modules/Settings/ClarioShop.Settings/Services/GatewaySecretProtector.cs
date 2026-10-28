using ClarioShop.SharedKernel.Tenancy;
using Microsoft.AspNetCore.DataProtection;

namespace ClarioShop.Settings.Services;

/// <summary>
/// Encrypts payment gateway secrets with the Data Protection API. The purpose names the tenant, so a value copied
/// into another tenant's database cannot be read there.
/// </summary>
internal sealed class GatewaySecretProtector(IDataProtectionProvider provider, ITenantContext tenant)
{
    private const string Purpose = "ClarioShop.Settings.PaymentGatewaySecrets";

    public string Protect(string secret) => Protector.Protect(secret);

    public string Unprotect(string protectedSecret) => Protector.Unprotect(protectedSecret);

    private IDataProtector Protector => provider.CreateProtector(Purpose, tenant.TenantId.ToString("N"));
}
