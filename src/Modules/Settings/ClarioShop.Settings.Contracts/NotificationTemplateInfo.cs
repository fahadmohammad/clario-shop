namespace ClarioShop.Settings.Contracts;

/// <summary>A store's own text for one message, in place of the built-in one.</summary>
/// <param name="Event">E.g. <c>OrderPlaced</c>.</param>
/// <param name="Channel"><c>Sms</c> or <c>Email</c>.</param>
/// <param name="Language">E.g. <c>bn</c> or <c>en</c>.</param>
/// <param name="Subject">Email only; null for SMS.</param>
public sealed record NotificationTemplateInfo(string Event, string Channel, string Language, string? Subject, string Body);
