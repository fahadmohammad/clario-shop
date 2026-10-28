using ClarioShop.Settings.Domain;
using ClarioShop.Settings.Persistence;
using ClarioShop.Settings.Services;
using Npgsql;

namespace ClarioShop.Settings.Features.PaymentGateways;

/// <summary>
/// Saves the store's SSLCommerz merchant account. The store password is required the first time; leave it out later
/// to keep the saved one. It is encrypted before it is stored and never returned.
/// </summary>
public static class UpdateSslCommerz
{
    public sealed record Command(string StoreId, string? StorePassword, bool UseSandbox, bool IsEnabled);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.StoreId).NotEmpty().MaximumLength(100).Matches("^[A-Za-z0-9_.-]+$")
                .WithMessage("Use the store id SSLCommerz gave you, e.g. clariomart0live.");
            RuleFor(command => command.StorePassword).NotEmpty().MaximumLength(200).When(command => command.StorePassword is not null);
        }
    }

    internal sealed class Handler(SettingsDbContext db, GatewaySecretProtector protector, TimeProvider clock) : ICommandHandler<Command, PaymentGatewayResponse>
    {
        public async Task<Result<PaymentGatewayResponse>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            var account = await db.PaymentGatewayAccounts.FirstOrDefaultAsync(a => a.Gateway == PaymentGateway.SslCommerz, cancellationToken);
            var protectedSecret = command.StorePassword is null ? null : protector.Protect(command.StorePassword);
            var now = clock.GetUtcNow();
            if (account is not null)
            {
                account.Update(command.StoreId, protectedSecret, command.UseSandbox, command.IsEnabled, now);
            }
            else if (protectedSecret is null)
            {
                return StoreSettingsErrors.GatewaySecretRequired;
            }
            else
            {
                account = PaymentGatewayAccount.Create(PaymentGateway.SslCommerz, command.StoreId, protectedSecret, command.UseSandbox, command.IsEnabled, now);
                db.PaymentGatewayAccounts.Add(account);
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception is DbUpdateConcurrencyException
                || exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                return StoreSettingsErrors.Concurrency;
            }

            return PaymentGatewayResponse.From(account);
        }
    }
}
