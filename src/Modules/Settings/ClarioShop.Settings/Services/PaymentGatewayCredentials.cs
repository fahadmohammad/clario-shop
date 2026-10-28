using ClarioShop.Settings.Contracts;
using ClarioShop.Settings.Domain;
using ClarioShop.Settings.Persistence;

namespace ClarioShop.Settings.Services;

internal sealed class PaymentGatewayCredentials(SettingsDbContext db, GatewaySecretProtector protector) : IPaymentGatewayCredentials
{
    public async Task<SslCommerzCredentials?> GetSslCommerzAsync(CancellationToken cancellationToken)
    {
        var account = await db.PaymentGatewayAccounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Gateway == PaymentGateway.SslCommerz && a.IsEnabled, cancellationToken);
        return account is null
            ? null
            : new SslCommerzCredentials(account.MerchantId, protector.Unprotect(account.ProtectedSecret), account.UseSandbox);
    }
}
