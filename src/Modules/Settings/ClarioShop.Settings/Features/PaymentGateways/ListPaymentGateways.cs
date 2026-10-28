using ClarioShop.Settings.Persistence;

namespace ClarioShop.Settings.Features.PaymentGateways;

/// <summary>The store's payment gateway accounts, switched off ones included.</summary>
public static class ListPaymentGateways
{
    public sealed record Query;

    internal sealed class Handler(SettingsDbContext db) : ICommandHandler<Query, IReadOnlyList<PaymentGatewayResponse>>
    {
        public async Task<Result<IReadOnlyList<PaymentGatewayResponse>>> HandleAsync(Query query, CancellationToken cancellationToken)
        {
            var accounts = await db.PaymentGatewayAccounts.AsNoTracking()
                .OrderBy(account => account.Gateway)
                .ToListAsync(cancellationToken);
            return accounts.Select(PaymentGatewayResponse.From).ToList();
        }
    }
}
