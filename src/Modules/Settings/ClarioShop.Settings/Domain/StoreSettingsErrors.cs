using ClarioShop.SharedKernel.Results;

namespace ClarioShop.Settings.Domain;

internal static class StoreSettingsErrors
{
    public static readonly Error Concurrency =
        Error.Conflict("settings.concurrency", "The settings changed while saving; reload them and try again.");

    public static readonly Error DefaultLanguageNotOffered =
        Error.Validation("defaultLanguage", "The default language must be one of the store's languages.");

    public static readonly Error DeliveryZoneNotFound =
        Error.NotFound("settings.delivery_zone_not_found", "The delivery zone does not exist.");

    public static readonly Error GatewaySecretRequired =
        Error.Validation("storePassword", "Enter the store password; it is required the first time the account is saved.");

    public static readonly Error NotificationTemplateNotFound =
        Error.NotFound("settings.notification_template_not_found", "There is no such message; list the notification templates to see which exist.");

    public static Error DeliveryZoneNameTaken(string name) =>
        Error.Conflict("settings.delivery_zone_name_taken", $"A delivery zone named '{name}' already exists.");
}
