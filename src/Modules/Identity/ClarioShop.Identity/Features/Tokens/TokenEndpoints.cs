using System.Security.Claims;
using ClarioShop.Identity.Services;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace ClarioShop.Identity.Features.Tokens;

internal sealed class TokenEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapPost($"/{TokenServerRegistration.TokenEndpoint}", ExchangeAsync)
            .WithTags("Identity")
            .WithName("IssueToken")
            .WithSummary("OAuth 2.0 token endpoint for staff and shopper sign-in and token refresh.")
            .WithDescription(
                "Form-encoded (application/x-www-form-urlencoded). grant_type=password with username (email) and password, " +
                $"grant_type={OtpGrant.GrantType} with phone and code (see POST /api/identity/otp), " +
                "or grant_type=refresh_token with refresh_token. Returns access_token (15 minutes), refresh_token " +
                "(rotated on every use) and expires_in. Failures are OAuth errors such as invalid_grant.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .RequireRateLimiting(IdentityModule.TokenRateLimitPolicy)
            .AllowAnonymous();

        app.MapPost("/api/identity/otp", RequestOtpAsync)
            .WithTags("Identity")
            .WithName("RequestOtp")
            .WithSummary("Send a shopper a one-time sign-in code by SMS.")
            .WithDescription(
                "Sends a 6-digit code to a Bangladeshi mobile number. Exchange it at the token endpoint with " +
                $"grant_type={OtpGrant.GrantType}, phone and code. A code expires after 3 minutes and allows 5 attempts; " +
                "only the newest code for a phone works. Codes per phone and per IP address are limited (429).")
            .WithValidation<RequestOtp.Request>()
            .Produces<RequestOtp.Response>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireRateLimiting(IdentityModule.TokenRateLimitPolicy)
            .AllowAnonymous();
    }

    private static async Task<IResult> RequestOtpAsync(
        RequestOtp.Request request,
        HttpContext context,
        RequestOtp.Handler handler,
        CancellationToken cancellationToken)
    {
        var address = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var result = await handler.HandleAsync(new RequestOtp.Command(request.Phone, address), cancellationToken);
        return result.ToHttpResult(sent => TypedResults.Accepted((string?)null, sent));
    }

    private static async Task<IResult> ExchangeAsync(
        HttpContext context,
        PasswordGrant.Handler passwordGrant,
        RefreshTokenGrant.Handler refreshTokenGrant,
        OtpGrant.Handler otpGrant,
        CancellationToken cancellationToken)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The token endpoint is only reachable through the OpenIddict server.");

        Result<ClaimsPrincipal> result;
        if (request.IsPasswordGrantType())
        {
            result = await passwordGrant.HandleAsync(new PasswordGrant.Command(request.Username ?? string.Empty, request.Password ?? string.Empty), cancellationToken);
        }
        else if (request.IsRefreshTokenGrantType())
        {
            var refreshToken = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            result = await refreshTokenGrant.HandleAsync(new RefreshTokenGrant.Command(refreshToken.Principal), cancellationToken);
        }
        else if (request.GrantType == OtpGrant.GrantType)
        {
            var phone = (string?)request.GetParameter("phone") ?? string.Empty;
            var code = (string?)request.GetParameter("code") ?? string.Empty;
            result = await otpGrant.HandleAsync(new OtpGrant.Command(phone, code), cancellationToken);
        }
        else
        {
            // OpenIddict rejects every grant type that is not enabled before the request gets here.
            throw new InvalidOperationException($"Grant type {request.GrantType} is not supported.");
        }

        return result.IsSuccess
            ? Results.SignIn(result.Value, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)
            : Results.Forbid(
                new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = result.Error.Message,
                }),
                [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
    }
}
