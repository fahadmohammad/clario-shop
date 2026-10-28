using ClarioShop.Notifications.Contracts;

namespace ClarioShop.Notifications.Features.Notifications;

internal sealed class NotificationEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notifications").WithTags("Notifications");

        group.MapGet("/", static async ([AsParameters] ListNotifications.Query query, ListNotifications.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(query, cancellationToken)).ToHttpResult())
            .WithName("ListNotifications")
            .WithSummary("SMS and email sent to shoppers, newest first, optionally for one order or in one status (e.g. Failed).")
            .WithValidation<ListNotifications.Query>()
            .Produces<ListNotifications.Response>()
            .RequirePermission(NotificationsPermissions.View);
    }
}
