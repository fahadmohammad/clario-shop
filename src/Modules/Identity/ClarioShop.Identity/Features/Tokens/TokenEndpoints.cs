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
            .WithSummary("OAuth 2.0 token endpoint for staff sign-in and token refresh.")
            .WithDescription(
                "Form-encoded (application/x-www-form-urlencoded). grant_type=password with username (email) and password, " +
                "or grant_type=refresh_token with refresh_token. Returns access_token (15 minutes), refresh_token " +
                "(rotated on every use) and expires_in. Failures are OAuth errors such as invalid_grant.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .RequireRateLimiting(IdentityModule.TokenRateLimitPolicy)
            .AllowAnonymous();
    }

    private static async Task<IResult> ExchangeAsync(
        HttpContext context,
        PasswordGrant.Handler passwordGrant,
        RefreshTokenGrant.Handler refreshTokenGrant,
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
