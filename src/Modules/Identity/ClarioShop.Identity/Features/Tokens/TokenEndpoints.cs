namespace ClarioShop.Identity.Features.Tokens;

internal sealed class TokenEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/identity").WithTags("Identity");

        group.MapPost("/token", static async (IssueToken.Command command, IssueToken.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(command, cancellationToken)).ToHttpResult())
            .WithName("IssueToken")
            .WithSummary("Sign in a staff member with email and password and get an access token.")
            .WithValidation<IssueToken.Command>()
            .Produces<IssueToken.Response>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireRateLimiting(IdentityModule.TokenRateLimitPolicy)
            .AllowAnonymous();
    }
}
