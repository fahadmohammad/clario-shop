using System.Security.Claims;
using ClarioShop.Identity.Contracts;

namespace ClarioShop.Identity.Features.Users;

internal sealed class UserEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/identity").WithTags("Identity");

        group.MapGet("/me", static async (ClaimsPrincipal principal, GetCurrentUser.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new GetCurrentUser.Query(principal), cancellationToken)).ToHttpResult())
            .WithName("GetCurrentUser")
            .WithSummary("The signed-in staff member or shopper and their permissions.")
            .Produces<GetCurrentUser.Response>()
            .RequireAuthorization();

        group.MapPost("/users", static async (CreateUser.Command command, CreateUser.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(command, cancellationToken))
                    .ToHttpResult(created => TypedResults.Created($"/api/identity/users/{created.Id}", created)))
            .WithName("CreateUser")
            .WithSummary("Create a staff account with one or more roles.")
            .WithValidation<CreateUser.Command>()
            .Produces<CreateUser.Response>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(IdentityPermissions.UsersManage);
    }
}
