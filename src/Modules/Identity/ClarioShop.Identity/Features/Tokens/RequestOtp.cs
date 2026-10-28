using System.Globalization;
using ClarioShop.Customers.Contracts;
using ClarioShop.Identity.Domain;
using ClarioShop.Identity.Persistence;
using ClarioShop.Identity.Services;
using ClarioShop.Infrastructure.Sms;
using Microsoft.Extensions.Options;

namespace ClarioShop.Identity.Features.Tokens;

/// <summary>Sends a shopper a one-time sign-in code by SMS; the code is then exchanged at the token endpoint.</summary>
public static class RequestOtp
{
    public sealed record Request(string Phone);

    public sealed record Command(string Phone, string ClientAddress);

    /// <param name="Phone">The number the code was sent to, in E.164.</param>
    public sealed record Response(string Phone, int ExpiresInSeconds);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator() => RuleFor(request => request.Phone).NotEmpty().MaximumLength(32);
    }

    internal sealed class Handler(
        IdentityDbContext db,
        ICustomerDirectory customers,
        OtpRequestThrottle throttle,
        ISmsSender sms,
        TimeProvider clock,
        IOptions<AuthOptions> options) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            var phone = customers.NormalizePhone(command.Phone);
            if (phone is null)
            {
                return Error.Validation(nameof(Request.Phone), "Enter a Bangladeshi mobile number, e.g. 01712-345678.");
            }

            if (!await throttle.TryAcquireAsync(phone, command.ClientAddress, cancellationToken))
            {
                return Error.TooManyRequests("identity.otp_rate_limited", "Too many codes requested. Wait a few minutes and try again.");
            }

            var lifetime = options.Value.Otp.CodeLifetime;
            var otp = OtpCode.Issue(phone, clock.GetUtcNow(), lifetime, out var code);
            db.OtpCodes.Add(otp);
            await db.SaveChangesAsync(cancellationToken);

            var minutes = (int)Math.Ceiling(lifetime.TotalMinutes);
            await sms.SendAsync(
                phone,
                string.Create(CultureInfo.InvariantCulture, $"Your sign-in code is {code}. It expires in {minutes} minutes. Never share it with anyone."),
                cancellationToken);
            return new Response(phone, (int)lifetime.TotalSeconds);
        }
    }
}
