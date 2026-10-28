using ClarioShop.Settings.Contracts;

namespace ClarioShop.Settings.Features.NotificationTemplates;

internal sealed class NotificationTemplateEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/settings/notification-templates").WithTags("Settings");

        group.MapGet("/", static async (ListNotificationTemplates.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new ListNotificationTemplates.Query(), cancellationToken)).ToHttpResult())
            .WithName("ListNotificationTemplates")
            .WithSummary("Every SMS and email the shopper can get, per event and language: the text the store sends, whether it is the store's own, its placeholders and the built-in text.")
            .Produces<IReadOnlyList<NotificationTemplateResponse>>()
            .RequirePermission(SettingsPermissions.Edit);

        group.MapPut("/{event}/{channel}/{language}", static async (
                    string @event,
                    string channel,
                    string language,
                    UpdateNotificationTemplate.Input template,
                    UpdateNotificationTemplate.Handler handler,
                    CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new UpdateNotificationTemplate.Command(@event, channel, language, template), cancellationToken)).ToHttpResult())
            .WithName("UpdateNotificationTemplate")
            .WithSummary("Use the store's own text for one message, e.g. /OrderShipped/Sms/bn. Emails need a subject; SMS have none. Placeholders such as {OrderNumber} must be ones the message has.")
            .WithValidation<UpdateNotificationTemplate.Input>()
            .Produces<NotificationTemplateResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(SettingsPermissions.Edit);

        group.MapDelete("/{event}/{channel}/{language}", static async (
                    string @event,
                    string channel,
                    string language,
                    ResetNotificationTemplate.Handler handler,
                    CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new ResetNotificationTemplate.Command(@event, channel, language), cancellationToken)).ToHttpResult())
            .WithName("ResetNotificationTemplate")
            .WithSummary("Go back to the built-in text for one message. Returns the message as it is now sent.")
            .Produces<NotificationTemplateResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequirePermission(SettingsPermissions.Edit);
    }
}
