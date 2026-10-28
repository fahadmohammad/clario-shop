namespace ClarioShop.Notifications.Contracts;

/// <summary>A message the shopper can get, with its built-in text.</summary>
/// <param name="Event">E.g. <c>OrderPlaced</c>.</param>
/// <param name="Channel"><c>Sms</c> or <c>Email</c>.</param>
/// <param name="Language">E.g. <c>bn</c> or <c>en</c>.</param>
/// <param name="Subject">Email only; null for SMS.</param>
/// <param name="Placeholders">The names a template for this message may use in braces, e.g. <c>OrderNumber</c> for <c>{OrderNumber}</c>.</param>
public sealed record NotificationTemplateDefinition(
    string Event,
    string Channel,
    string Language,
    string? Subject,
    string Body,
    IReadOnlyList<string> Placeholders);
