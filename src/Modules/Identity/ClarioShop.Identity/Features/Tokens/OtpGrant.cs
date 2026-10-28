using System.Security.Claims;
using ClarioShop.Customers.Contracts;
using ClarioShop.Identity.Domain;
using ClarioShop.Identity.Persistence;
using ClarioShop.Identity.Services;
using Microsoft.Extensions.Options;
using Npgsql;

namespace ClarioShop.Identity.Features.Tokens;

/// <summary>
/// The OTP grant: a shopper signs in with their phone and the code sent to it. The first sign-in creates the
/// shopper's account, linked to the Customers module's customer for that phone (created if new).
/// </summary>
public static class OtpGrant
{
    /// <summary>The grant_type value; the request also carries <c>phone</c> and <c>code</c>.</summary>
    public const string GrantType = "urn:clarioshop:params:oauth:grant-type:otp";

    /// <summary>Attempts before giving up when concurrent first sign-ins race to create the same account.</summary>
    private const int MaxSaveAttempts = 3;

    public sealed record Command(string Phone, string Code);

    internal sealed class Handler(
        IdentityDbContext db,
        ICustomerDirectory customers,
        TokenPrincipalFactory principals,
        TimeProvider clock,
        IOptions<AuthOptions> options) : ICommandHandler<Command, ClaimsPrincipal>
    {
        private static readonly Error InvalidCode =
            Error.Unauthorized("identity.otp_invalid", "The code is incorrect or has expired. Request a new code.");

        private static readonly Error AttemptsExhausted =
            Error.Unauthorized("identity.otp_attempts_exhausted", "Too many wrong codes. Request a new code.");

        private static readonly Error AccountDisabled =
            Error.Unauthorized("identity.account_disabled", "This account is disabled.");

        public async Task<Result<ClaimsPrincipal>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            var phone = customers.NormalizePhone(command.Phone);
            if (phone is null)
            {
                return InvalidCode;
            }

            var redeemed = await RedeemAsync(phone, command.Code, cancellationToken);
            if (redeemed.IsFailure)
            {
                return redeemed.Error;
            }

            var customer = await customers.FindOrCreateByPhoneAsync(phone, name: null, cancellationToken);
            if (customer.IsFailure)
            {
                return customer.Error;
            }

            var user = await FindOrCreateShopperAsync(customer.Value, cancellationToken);
            if (!user.IsActive)
            {
                return AccountDisabled;
            }

            return await principals.CreateAsync(user, cancellationToken);
        }

        /// <summary>
        /// Checks the code against the phone's newest code. Every check uses up an attempt and the code is consumed
        /// by conditional updates, so concurrent requests can neither exceed the attempt limit nor use a code twice.
        /// </summary>
        private async Task<Result> RedeemAsync(string phone, string code, CancellationToken cancellationToken)
        {
            var now = clock.GetUtcNow();
            var otp = await db.OtpCodes.AsNoTracking()
                .Where(o => o.Phone == phone)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (otp is null || otp.ConsumedAt is not null || otp.IsExpired(now))
            {
                return InvalidCode;
            }

            var maxAttempts = options.Value.Otp.MaxAttempts;
            var counted = await db.OtpCodes
                .Where(o => o.Id == otp.Id && o.ConsumedAt == null && o.Attempts < maxAttempts)
                .ExecuteUpdateAsync(setters => setters.SetProperty(o => o.Attempts, o => o.Attempts + 1), cancellationToken);
            if (counted == 0)
            {
                return AttemptsExhausted;
            }

            if (!otp.Matches(code))
            {
                return InvalidCode;
            }

            var consumed = await db.OtpCodes
                .Where(o => o.Id == otp.Id && o.ConsumedAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(o => o.ConsumedAt, now), cancellationToken);
            return consumed == 1 ? Result.Success() : InvalidCode;
        }

        private async Task<User> FindOrCreateShopperAsync(CustomerInfo customer, CancellationToken cancellationToken)
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    var user = await db.Users.Include(u => u.Roles).SingleOrDefaultAsync(u => u.Phone == customer.Phone, cancellationToken);
                    if (user is null)
                    {
                        user = User.CreateShopper(customer.Phone, customer.CustomerId, customer.Name, clock.GetUtcNow());
                        db.Users.Add(user);
                        await db.SaveChangesAsync(cancellationToken);
                    }
                    else if (user.CustomerId != customer.CustomerId)
                    {
                        user.LinkCustomer(customer.CustomerId);
                        await db.SaveChangesAsync(cancellationToken);
                    }

                    return user;
                }
                catch (DbUpdateException exception)
                    when (attempt < MaxSaveAttempts && exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
                {
                    // A concurrent first sign-in for this phone created the account; read it again.
                    db.ChangeTracker.Clear();
                }
            }
        }
    }
}
