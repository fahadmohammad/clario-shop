using ClarioShop.Notifications.Contracts;
using ClarioShop.Settings.Domain;

namespace ClarioShop.Settings.Features.NotificationTemplates;

/// <summary>A message the shopper can get, with the text the store sends and the built-in text it replaces.</summary>
/// <param name="Event">E.g. <c>OrderPlaced</c>.</param>
/// <param name="Channel"><c>Sms</c> or <c>Email</c>.</param>
/// <param name="Language">E.g. <c>bn</c> or <c>en</c>.</param>
/// <param name="Subject">Email only; null for SMS.</param>
/// <param name="IsCustomized">True when the store wrote its own text; false when it sends the built-in one.</param>
/// <param name="Placeholders">The names the text may use in braces, e.g. <c>OrderNumber</c> for <c>{OrderNumber}</c>.</param>
/// <param name="UpdatedAt">When the store's own text was last saved; null when it has none.</param>
public sealed record NotificationTemplateResponse(
    string Event,
    string Channel,
    string Language,
    string? Subject,
    string Body,
    bool IsCustomized,
    IReadOnlyList<string> Placeholders,
    string? DefaultSubject,
    string DefaultBody,
    DateTimeOffset? UpdatedAt)
{
    internal static NotificationTemplateResponse From(NotificationTemplateDefinition definition, NotificationTemplate? custom) => new(
        definition.Event,
        definition.Channel,
        definition.Language,
        custom is null ? definition.Subject : custom.Subject,
        custom?.Body ?? definition.Body,
        custom is not null,
        definition.Placeholders,
        definition.Subject,
        definition.Body,
        custom?.UpdatedAt);
}
