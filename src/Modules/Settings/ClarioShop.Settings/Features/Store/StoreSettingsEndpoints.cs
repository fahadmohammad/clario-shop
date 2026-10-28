using ClarioShop.Settings.Contracts;

namespace ClarioShop.Settings.Features.Store;

internal sealed class StoreSettingsEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/settings").WithTags("Settings");

        group.MapGet("/storefront", static async (GetStorefront.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new GetStorefront.Query(), cancellationToken)).ToHttpResult())
            .WithName("GetStorefrontSettings")
            .WithSummary("Public store name, contacts, theme tokens, currency format, languages and active delivery zones.")
            .Produces<GetStorefront.Response>()
            .AllowAnonymous();

        group.MapGet("/", static async (GetStoreSettings.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new GetStoreSettings.Query(), cancellationToken)).ToHttpResult())
            .WithName("GetStoreSettings")
            .WithSummary("All store settings. A store that never saved any gets the defaults, named after the tenant.")
            .Produces<StoreSettingsInfo>()
            .RequirePermission(SettingsPermissions.Edit);

        group.MapPut("/store", static async (UpdateStoreSettings.Command command, UpdateStoreSettings.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(command, cancellationToken)).ToHttpResult())
            .WithName("UpdateStoreSettings")
            .WithSummary("Replace the store profile, invoice prefix, VAT rate (percent), currency format and languages.")
            .WithValidation<UpdateStoreSettings.Command>()
            .Produces<StoreSettingsInfo>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(SettingsPermissions.Edit);

        group.MapPut("/theme", static async (UpdateTheme.Command command, UpdateTheme.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(command, cancellationToken)).ToHttpResult())
            .WithName("UpdateTheme")
            .WithSummary("Replace the storefront theme: #RRGGBB colours, radius in rem, font family names, https logo and favicon URLs.")
            .WithValidation<UpdateTheme.Command>()
            .Produces<StoreSettingsInfo>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(SettingsPermissions.Edit);
    }
}
