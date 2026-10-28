using ClarioShop.Payments.Contracts;
using ClarioShop.Payments.Domain;
using ClarioShop.Settings.Contracts;

namespace ClarioShop.Payments.Services;

internal sealed class PaymentMethods(IPaymentGatewayCredentials credentials) : IPaymentMethods
{
    public async Task<bool> IsAvailableAsync(string paymentMethod, CancellationToken cancellationToken) =>
        Enum.TryParse<PaymentMethod>(paymentMethod, out var method) && Enum.IsDefined(method) && method switch
        {
            PaymentMethod.CashOnDelivery => true,
            PaymentMethod.SslCommerz => await credentials.GetSslCommerzAsync(cancellationToken) is not null,
            _ => false,
        };
}
